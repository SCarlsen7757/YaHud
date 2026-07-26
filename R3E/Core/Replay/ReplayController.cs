using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Recording;
using System.Diagnostics;

namespace R3E.Core.Replay
{
    /// <summary>
    /// Drives replay playback: transport controls, seeking, and catch-up progress for the UI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Structured as a <em>target-seeking pump</em>, not a queue of seek commands. A single replay
    /// thread owns playback and continuously moves <see cref="Position"/> toward
    /// <see cref="TargetPosition"/>, which the UI writes. Seeking is therefore just an assignment, and
    /// a new target automatically supersedes an in-flight catch-up — drags coalesce for free, with no
    /// cancellation bookkeeping or stale-seek queue.
    /// </para>
    /// <para>
    /// Seeking is asymmetric: a target ahead of the position consumes frames forward with no reset,
    /// because state at the current position is already correct; a target behind resets and consumes
    /// from file start. Either way YaHud only ever sees a monotonically forward frame stream, so no
    /// feature service needs rewind awareness. Because recording splits per session, file start is
    /// session start, which makes the backward case trivial.
    /// </para>
    /// <para>
    /// Pacing uses a dedicated thread with a <see cref="System.Diagnostics.Stopwatch"/>, sleeping to
    /// <c>target − 2 ms</c> and then spinning; <c>Task.Delay</c> resolution is too coarse for 60 Hz.
    /// </para>
    /// <para>
    /// Threading: the UI thread only ever writes interlocked scalars and signals
    /// <see cref="wake"/> — it never blocks on the pump, and the pump never takes a lock the UI
    /// holds. Only <see cref="Load"/>, <see cref="Unload"/> and <see cref="Dispose"/> synchronise,
    /// and they do so by stopping the pump thread outright, which keeps the loop itself provably
    /// single-threaded.
    /// </para>
    /// </remarks>
    public sealed class ReplayController : IDisposable
    {
        /// <summary>
        /// Catch-ups estimated longer than this suppress widget rendering, so a long rewind does not
        /// flood the Blazor dispatcher. Shorter scrubs keep rendering, which reads as fast-forward.
        /// </summary>
        public static readonly TimeSpan RenderSuppressionThreshold = TimeSpan.FromSeconds(2);

        /// <summary>Lowest accepted <see cref="Speed"/>.</summary>
        public const double MinSpeed = 0.1d;

        /// <summary>Highest accepted <see cref="Speed"/>.</summary>
        public const double MaxSpeed = 8d;

        /// <summary>Longest single wait the pump takes, so control changes are picked up promptly.</summary>
        private static readonly TimeSpan IdleWait = TimeSpan.FromMilliseconds(50);

        /// <summary>Tail of a frame wait that is spun rather than slept; OS timers are too coarse.</summary>
        private static readonly TimeSpan SpinThreshold = TimeSpan.FromMilliseconds(2);

        /// <summary>Throttle for progress-only <see cref="StateChanged"/> notifications.</summary>
        private static readonly TimeSpan ProgressNotifyInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Seed for the recorded-seconds-per-wall-second catch-up rate, refined by measurement after
        /// the first catch-up. ~100 µs/frame at 60 Hz recorded gives roughly this.
        /// </summary>
        private const double DefaultCatchUpRatio = 150d;

        private readonly SharedSourceSwitch sourceSwitch;
        private readonly ILogger<ReplayController> logger;

        /// <summary>Serialises load/unload/dispose. The pump thread never acquires it.</summary>
        private readonly object controlGate = new();

        /// <summary>Signalled by every control change so the pump abandons whatever it is waiting on.</summary>
        private readonly ManualResetEventSlim wake = new(false);

        private readonly Stopwatch clock = Stopwatch.StartNew();

        private Thread? pump;
        private CancellationTokenSource? pumpCts;

        private long targetTicks;
        private long positionTicks;
        private long durationTicks;
        private long frames;
        private long speedBits = BitConverter.DoubleToInt64Bits(1d);
        private long catchUpProgressBits;

        /// <summary>
        /// Bumped by every control change. The pump uses it to re-anchor its playback clock and to
        /// abandon an in-flight frame wait, which is what makes a new target supersede an old one.
        /// </summary>
        private long controlGeneration;

        private int steps;

        private volatile bool loaded;
        private volatile bool playing;
        private volatile bool catchingUp;
        private volatile bool suppressing;
        private volatile IReadOnlyList<RecordingMarker> lapMarkers = [];

        private bool disposed;

        // Pump-thread-only state.
        private double catchUpRatio = DefaultCatchUpRatio;
        private TimeSpan catchUpFrom;
        private TimeSpan catchUpTo;
        private TimeSpan catchUpStartedAt;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="sourceSwitch">The switch used to activate replay and return to live.</param>
        /// <param name="logger">Optional logger.</param>
        public ReplayController(SharedSourceSwitch sourceSwitch, ILogger<ReplayController>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(sourceSwitch);

            this.sourceSwitch = sourceSwitch;
            this.logger = logger ?? NullLogger<ReplayController>.Instance;
        }

        /// <summary>
        /// Raised whenever transport state, position or catch-up progress changes, so the UI can
        /// re-render.
        /// </summary>
        public event Action? StateChanged;

        /// <summary><see langword="true"/> when a recording is loaded and replay is active.</summary>
        public bool IsLoaded => loaded;

        /// <summary><see langword="true"/> while the transport is running.</summary>
        public bool IsPlaying => playing;

        /// <summary>Path of the loaded recording, or <see langword="null"/>.</summary>
        public string? CurrentFile => sourceSwitch.Replay.CurrentFile;

        /// <summary>Header of the loaded recording, or <see langword="null"/>.</summary>
        public TelemetryRecordingHeader? Header => sourceSwitch.Replay.Reader?.Header;

        /// <summary>Playback rate multiplier, clamped to <c>[0.1, 8]</c>.</summary>
        public double Speed
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref speedBits));
            set
            {
                var clamped = double.IsNaN(value) ? 1d : Math.Clamp(value, MinSpeed, MaxSpeed);
                Interlocked.Exchange(ref speedBits, BitConverter.DoubleToInt64Bits(clamped));
                SignalControlChange();
                RaiseStateChanged();
            }
        }

        /// <summary>Current playback position.</summary>
        public TimeSpan Position => new(Interlocked.Read(ref positionTicks));

        /// <summary>Position the pump is moving toward, clamped to <c>[0, <see cref="Duration"/>]</c>.</summary>
        public TimeSpan TargetPosition => new(Interlocked.Read(ref targetTicks));

        /// <summary>Total duration of the loaded recording.</summary>
        public TimeSpan Duration => new(Interlocked.Read(ref durationTicks));

        /// <summary>Frames emitted since the recording was loaded or last reset.</summary>
        public long FrameIndex => Interlocked.Read(ref frames);

        /// <summary><see langword="true"/> while the pump is consuming frames faster than real time to reach a target.</summary>
        public bool IsCatchingUp => catchingUp;

        /// <summary>Progress of the running catch-up, <c>0</c>–<c>1</c>.</summary>
        public double CatchUpProgress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref catchUpProgressBits));

        /// <summary>
        /// <see langword="true"/> while widgets should skip rendering, set during a catch-up
        /// estimated longer than <see cref="RenderSuppressionThreshold"/>.
        /// </summary>
        public bool SuppressRendering => suppressing;

        /// <summary>Lap-start markers of the loaded recording, for the jump-to-lap control.</summary>
        public IReadOnlyList<RecordingMarker> LapMarkers => lapMarkers;

        /// <summary>
        /// Loads a recording and activates replay mode, positioned at the start and paused.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        public void Load(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            lock (controlGate)
            {
                StopPump();
                ResetTransportState();
                lapMarkers = [];

                // Stay unloaded until the file is open: a bad recording must leave the controller
                // inert rather than half-loaded.
                loaded = false;
                sourceSwitch.ActivateReplay(path);

                var reader = sourceSwitch.Replay.Reader;
                Interlocked.Exchange(ref durationTicks, reader?.Duration.Ticks ?? 0L);
                lapMarkers = reader is null
                    ? []
                    : [.. reader.Markers.Where(m => m.Type == RecordingMarkerType.LapStart)];

                loaded = true;
                StartPump();
            }

            logger.LogInformation("Replay loaded '{Path}' ({Duration})", path, Duration);
            RaiseStateChanged();
        }

        /// <summary>Unloads the recording and returns the switch to the live source.</summary>
        public void Unload()
        {
            lock (controlGate)
            {
                StopPump();

                // Clear the suppression flag before anything else can observe it: a replay stopped
                // mid-catch-up must never leave widgets permanently refusing to render.
                ResetTransportState();
                lapMarkers = [];
                loaded = false;

                sourceSwitch.ActivateLive();
            }

            logger.LogInformation("Replay unloaded; live source restored");
            RaiseStateChanged();
        }

        /// <summary>Starts or resumes playback.</summary>
        public void Play()
        {
            if (!loaded)
            {
                return;
            }

            playing = true;
            SignalControlChange();
            RaiseStateChanged();
        }

        /// <summary>Pauses playback, holding the last frame.</summary>
        public void Pause()
        {
            playing = false;
            SignalControlChange();
            RaiseStateChanged();
        }

        /// <summary>
        /// Sets the pump target. Supersedes any in-flight catch-up; play/pause state is preserved.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        public void Seek(TimeSpan position)
        {
            if (!loaded)
            {
                return;
            }

            var duration = Duration;
            var clamped = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > duration ? duration : position;

            Interlocked.Exchange(ref targetTicks, clamped.Ticks);
            SignalControlChange();
            RaiseStateChanged();
        }

        /// <summary>
        /// Advances a fixed number of frames while paused.
        /// </summary>
        /// <param name="frames">Frames to advance; must be positive.</param>
        public void Step(int frames = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);

            if (!loaded)
            {
                return;
            }

            Interlocked.Add(ref steps, frames);
            SignalControlChange();
        }

        /// <summary>
        /// Seeks to the start of a lap, using the recording's <c>LapStart</c> markers.
        /// </summary>
        /// <param name="lap">Completed-lap count to jump to.</param>
        public void JumpToLap(int lap)
        {
            var reader = sourceSwitch.Replay.Reader;
            if (reader is null)
            {
                return;
            }

            if (reader.TryGetLapStart(lap, out var position))
            {
                Seek(position);
            }
            else if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("No lap-start marker for lap {Lap}", lap);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (controlGate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                StopPump();
                ResetTransportState();
                lapMarkers = [];
                loaded = false;
            }

            wake.Dispose();
            StateChanged = null;
        }

        /// <summary>
        /// Clears everything a stale recording could leave behind. Deliberately clears
        /// <see cref="SuppressRendering"/> and <see cref="IsCatchingUp"/> so tearing replay down
        /// mid-catch-up cannot leave the HUD frozen.
        /// </summary>
        private void ResetTransportState()
        {
            playing = false;
            catchingUp = false;
            suppressing = false;
            Interlocked.Exchange(ref catchUpProgressBits, BitConverter.DoubleToInt64Bits(0d));
            Interlocked.Exchange(ref positionTicks, 0L);
            Interlocked.Exchange(ref targetTicks, 0L);
            Interlocked.Exchange(ref durationTicks, 0L);
            Interlocked.Exchange(ref frames, 0L);
            Interlocked.Exchange(ref steps, 0);
        }

        private void StartPump()
        {
            pumpCts = new CancellationTokenSource();
            var token = pumpCts.Token;

            pump = new Thread(() => PumpLoop(token))
            {
                IsBackground = true,
                Name = "YaHud replay pump",
                Priority = ThreadPriority.AboveNormal,
            };

            pump.Start();
        }

        private void StopPump()
        {
            var thread = pump;
            var cts = pumpCts;

            pump = null;
            pumpCts = null;

            if (thread is null)
            {
                return;
            }

            cts?.Cancel();
            wake.Set();

            // The pump checks cancellation between individual frames, so even a running catch-up
            // exits within roughly one frame's work.
            if (!thread.Join(TimeSpan.FromSeconds(5)))
            {
                logger.LogWarning("Replay pump did not stop within the timeout");
            }

            cts?.Dispose();
            wake.Reset();
        }

        private void SignalControlChange()
        {
            Interlocked.Increment(ref controlGeneration);
            wake.Set();
        }

        /// <summary>
        /// The target-seeking pump. One iteration does one unit of work and then re-reads the
        /// target, which is what makes a new target supersede an in-flight catch-up without any
        /// cancellation bookkeeping.
        /// </summary>
        private void PumpLoop(CancellationToken token)
        {
            var source = sourceSwitch.Replay;
            var generation = Interlocked.Read(ref controlGeneration);
            var anchorReal = clock.Elapsed;
            var anchorPos = TimeSpan.Zero;
            var needsAnchor = true;
            var lastNotify = clock.Elapsed;

            while (!token.IsCancellationRequested)
            {
                wake.Reset();

                var current = Interlocked.Read(ref controlGeneration);
                if (current != generation)
                {
                    generation = current;
                    needsAnchor = true;
                }

                var target = TargetPosition;
                var position = source.Position;

                // 1. Target behind position: reset and rewind to file start. Files are split per
                //    session, so file start is session start — there is no marker to look up. The
                //    reset itself reaches the feature services through the frame stream: the first
                //    frame after the rewind carries a lower GameSimulationTicks than the last one
                //    emitted, which is exactly the condition TelemetryService resets on.
                if (target < position)
                {
                    RewindToStart(source);
                    needsAnchor = true;
                    continue;
                }

                // 2. Target ahead of position: consume forward, one frame per iteration, with no
                //    reset — state at the current position is already correct. Frames are never
                //    skipped: level-triggered detection tolerates gaps for most state, but skipping
                //    a lap or sector boundary would lose a transition.
                if (target > position)
                {
                    if (!catchingUp)
                    {
                        BeginCatchUp(position, target);
                    }

                    if (!source.EmitNext())
                    {
                        HandleEndOfFile(source);
                        continue;
                    }

                    Interlocked.Increment(ref frames);
                    var landed = source.Position;
                    Interlocked.Exchange(ref positionTicks, landed.Ticks);
                    UpdateCatchUpProgress(landed);
                    MaybeNotify(ref lastNotify);

                    // Frame timestamps are discrete and rarely coincide exactly with an arbitrary
                    // seek target, so the frame that finally reaches it typically overshoots by a
                    // few milliseconds. Snap the target onto wherever we actually landed in this same
                    // step - deferring the snap to case 3 on the next iteration would leave a window
                    // where the top-of-loop check reads target < position and mistakes our own
                    // overshoot for a brand new backward seek, rewinding to file start and repeating
                    // this forever (the target is unchanged, so it overshoots the same way every
                    // time).
                    if (landed.Ticks >= target.Ticks)
                    {
                        Interlocked.CompareExchange(ref targetTicks, landed.Ticks, target.Ticks);
                    }

                    continue;
                }

                // 3. Caught up.
                if (catchingUp)
                {
                    EndCatchUp();
                    needsAnchor = true;
                }

                // Snap the target onto the frame actually landed on. A target that fell between two
                // frames would otherwise sit marginally behind the position and be re-read as a
                // backward seek on the next iteration. The compare-exchange makes the snap lose to a
                // seek issued concurrently by the UI.
                Interlocked.CompareExchange(ref targetTicks, position.Ticks, target.Ticks);

                if (!playing)
                {
                    if (TryTakeStep())
                    {
                        if (!source.EmitNext())
                        {
                            HandleEndOfFile(source);
                            continue;
                        }

                        Interlocked.Increment(ref frames);
                        var stepped = source.Position;
                        Interlocked.Exchange(ref positionTicks, stepped.Ticks);
                        Interlocked.CompareExchange(ref targetTicks, stepped.Ticks, position.Ticks);
                        needsAnchor = true;
                        RaiseStateChanged();
                        continue;
                    }

                    wake.Wait(IdleWait);
                    continue;
                }

                // 4. Playing at real-time cadence: pace off the recording's own inter-frame deltas.
                if (!source.TryPeekNext(out var nextPosition))
                {
                    HandleEndOfFile(source);
                    continue;
                }

                if (needsAnchor)
                {
                    anchorReal = clock.Elapsed;
                    anchorPos = position;
                    needsAnchor = false;
                }

                var speed = Speed;
                var due = anchorReal + TimeSpan.FromTicks((long)((nextPosition - anchorPos).Ticks / speed));

                if (!WaitUntil(due, generation, token))
                {
                    continue;
                }

                if (!source.EmitNext())
                {
                    HandleEndOfFile(source);
                    continue;
                }

                Interlocked.Increment(ref frames);
                var emitted = source.Position;
                Interlocked.Exchange(ref positionTicks, emitted.Ticks);
                Interlocked.CompareExchange(ref targetTicks, emitted.Ticks, position.Ticks);
                MaybeNotify(ref lastNotify);
            }
        }

        /// <summary>
        /// Waits until <paramref name="due"/>, sleeping to <see cref="SpinThreshold"/> before it and
        /// spinning the rest — <c>Task.Delay</c> and thread sleeps are far too coarse for 60 Hz.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the wait was abandoned because a control change superseded
        /// it or the pump is stopping.
        /// </returns>
        private bool WaitUntil(TimeSpan due, long generation, CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested || Interlocked.Read(ref controlGeneration) != generation)
                {
                    return false;
                }

                var remaining = due - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return true;
                }

                if (remaining > SpinThreshold)
                {
                    var slice = remaining - SpinThreshold;
                    if (slice > IdleWait)
                    {
                        slice = IdleWait;
                    }

                    // Every control change sets the wake handle, so this returns immediately when a
                    // seek arrives mid-wait.
                    wake.Wait(Math.Max(1, (int)slice.TotalMilliseconds));
                    continue;
                }

                Thread.SpinWait(50);
            }
        }

        private void RewindToStart(FileSharedSource source)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Backward seek from {Position}: rewinding to file start", source.Position);
            }

            // Only ever zero. Repositioning the reader anywhere else would put a forward jump into
            // the stream, which is precisely what every feature service is built to not have to
            // survive.
            source.SeekTo(TimeSpan.Zero);

            Interlocked.Exchange(ref positionTicks, 0L);
            Interlocked.Exchange(ref frames, 0L);

            // Force the forward branch to establish a fresh catch-up baseline from zero.
            catchingUp = false;
        }

        private bool TryTakeStep()
        {
            while (true)
            {
                var pending = Volatile.Read(ref steps);
                if (pending <= 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref steps, pending - 1, pending) == pending)
                {
                    return true;
                }
            }
        }

        private void BeginCatchUp(TimeSpan from, TimeSpan to)
        {
            catchUpFrom = from;
            catchUpTo = to;
            catchUpStartedAt = clock.Elapsed;
            catchingUp = true;
            SetCatchUpProgress(0d);

            var span = to - from;
            var estimated = span > TimeSpan.Zero
                ? TimeSpan.FromSeconds(span.TotalSeconds / catchUpRatio)
                : TimeSpan.Zero;

            // Short forward scrubs keep rendering: they read as a fast-forward, which looks better
            // than a frozen HUD. Only a long catch-up would flood the Blazor dispatcher.
            suppressing = estimated > RenderSuppressionThreshold;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Catching up {Span} from {From} (estimated {Estimated}, rendering {State})",
                    span, from, estimated, suppressing ? "suppressed" : "live");
            }

            RaiseStateChanged();
        }

        private void UpdateCatchUpProgress(TimeSpan position)
        {
            var span = (catchUpTo - catchUpFrom).TotalSeconds;
            SetCatchUpProgress(span > 0d
                ? Math.Clamp((position - catchUpFrom).TotalSeconds / span, 0d, 1d)
                : 1d);

            // The estimate can be wrong on the first catch-up of a session. Escalate to suppression
            // once the catch-up has actually outrun the threshold.
            if (!suppressing && clock.Elapsed - catchUpStartedAt > RenderSuppressionThreshold)
            {
                suppressing = true;
                RaiseStateChanged();
            }
        }

        private void EndCatchUp()
        {
            var wall = clock.Elapsed - catchUpStartedAt;
            var span = catchUpTo - catchUpFrom;

            if (span > TimeSpan.Zero && wall > TimeSpan.FromMilliseconds(50))
            {
                // Exponential moving average, so the suppression estimate self-corrects to the
                // machine it is actually running on.
                var observed = span.TotalSeconds / wall.TotalSeconds;
                catchUpRatio = (catchUpRatio * 0.7d) + (observed * 0.3d);
            }

            catchingUp = false;
            suppressing = false;
            SetCatchUpProgress(1d);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Caught up {Span} in {Wall} ({Ratio:F0}x)", span, wall, catchUpRatio);
            }

            RaiseStateChanged();
        }

        private void HandleEndOfFile(FileSharedSource source)
        {
            var position = source.Position;
            Interlocked.Exchange(ref positionTicks, position.Ticks);

            // Pull a target that is beyond the last record back onto it, otherwise the pump spins
            // trying to reach a position the file does not contain. A concurrent *backward* seek is
            // behind the position and is deliberately left alone.
            var target = Interlocked.Read(ref targetTicks);
            if (target > position.Ticks)
            {
                Interlocked.CompareExchange(ref targetTicks, position.Ticks, target);
            }

            if (catchingUp)
            {
                EndCatchUp();
            }

            if (playing)
            {
                playing = false;
                logger.LogInformation("Replay reached the end of '{Path}'", CurrentFile);
            }

            RaiseStateChanged();
        }

        private void SetCatchUpProgress(double value)
            => Interlocked.Exchange(ref catchUpProgressBits, BitConverter.DoubleToInt64Bits(value));

        private void MaybeNotify(ref TimeSpan lastNotify)
        {
            var now = clock.Elapsed;
            if (now - lastNotify < ProgressNotifyInterval)
            {
                return;
            }

            lastNotify = now;
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // A throwing UI handler must not be able to kill the pump thread.
                logger.LogWarning(ex, "A replay StateChanged handler threw");
            }
        }
    }
}
