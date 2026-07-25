using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Interfaces;
using R3E.Data;

namespace R3E.Core.Replay
{
    /// <summary>
    /// The composite <see cref="ISharedSource"/> that makes live↔replay switchable at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered as the singleton <see cref="ISharedSource"/>, it holds the live source (shared
    /// memory or UDP, chosen by the existing OS/<c>--force-udp</c> logic) and a
    /// <see cref="FileSharedSource"/>, and forwards events from whichever is active.
    /// </para>
    /// <para>
    /// This matters because <see cref="ISharedSource"/> is otherwise bound at DI registration time.
    /// <c>ITelemetryService</c> subscribes to the switch once at construction and never notices a
    /// swap, so mode switching costs no restart. Recording is disabled while replaying.
    /// </para>
    /// <para>
    /// Exactly one source is subscribed at any moment. The inactive source's events are detached, so
    /// a game running in the background cannot leak frames into a replay, and a subscription can
    /// never survive a swap or be attached twice.
    /// </para>
    /// </remarks>
    public sealed class SharedSourceSwitch : ISharedSource, IDisposable
    {
        private readonly ILogger<SharedSourceSwitch> logger;

        /// <summary>Serialises swaps against each other; the forwarding path itself takes no lock.</summary>
        private readonly object gate = new();

        // Cached delegates so subscribe/unsubscribe are provably symmetric.
        private readonly Action<Shared> onDataUpdated;
        private readonly Action<ReadOnlyMemory<byte>> onRawFrameReceived;
        private readonly Action<int> onStartLightsChanged;

        private ISharedSource active;
        private Shared data;
        private bool disposed;

        /// <summary>
        /// Creates the switch, starting in live mode.
        /// </summary>
        /// <param name="liveSource">The live source, already registered as a hosted service.</param>
        /// <param name="replaySource">The file-backed source used for replay.</param>
        /// <param name="logger">Optional logger.</param>
        public SharedSourceSwitch(ISharedSource liveSource, FileSharedSource replaySource, ILogger<SharedSourceSwitch>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(liveSource);
            ArgumentNullException.ThrowIfNull(replaySource);

            this.logger = logger ?? NullLogger<SharedSourceSwitch>.Instance;

            Live = liveSource;
            Replay = replaySource;

            onDataUpdated = ForwardDataUpdated;
            onRawFrameReceived = ForwardRawFrame;
            onStartLightsChanged = ForwardStartLights;

            data = new();
            active = liveSource;
            Subscribe(liveSource);
        }

        /// <inheritdoc />
        public event Action<Shared>? DataUpdated;

        /// <inheritdoc />
        public event Action<ReadOnlyMemory<byte>>? RawFrameReceived;

        /// <inheritdoc />
        public event Action<int>? StartLightsChanged;

        /// <summary>Raised after the active source changes, so consumers can react to the mode swap.</summary>
        /// <remarks>
        /// Raised after the old source is detached and the new one attached, but before any frame
        /// from the new source is forwarded. This is the switch's half of the reset path from
        /// section 6 of the plan: accumulated feature state belongs to the source that just went
        /// away and must be discarded here.
        /// </remarks>
        public event Action? ActiveSourceChanged;

        /// <inheritdoc />
        public Shared Data => data;

        /// <summary>The source currently being forwarded.</summary>
        public ISharedSource Active => active;

        /// <summary>The live source, regardless of which one is active.</summary>
        public ISharedSource Live { get; }

        /// <summary>The file-backed replay source, regardless of which one is active.</summary>
        public FileSharedSource Replay { get; }

        /// <summary><see langword="true"/> while the replay source is active.</summary>
        public bool IsReplaying => ReferenceEquals(active, Replay);

        /// <summary>
        /// Switches back to the live source. Unloads the replay file and raises the reset path so
        /// accumulated feature state is discarded.
        /// </summary>
        public void ActivateLive()
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            lock (gate)
            {
                if (ReferenceEquals(active, Live))
                {
                    return;
                }

                Unsubscribe(Replay);
                Replay.Unload();

                data = new();
                active = Live;
                Subscribe(Live);
            }

            logger.LogInformation("Switched to the live telemetry source");
            ActiveSourceChanged?.Invoke();
        }

        /// <summary>
        /// Loads a recording and switches to the replay source. Raises the reset path so accumulated
        /// feature state is discarded.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        public void ActivateReplay(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            lock (gate)
            {
                var wasReplaying = ReferenceEquals(active, Replay);

                // Detach the live source before loading, so a running game cannot interleave a frame
                // with the recording while the swap is in progress.
                if (!wasReplaying)
                {
                    Unsubscribe(Live);
                }
                else
                {
                    // Re-loading a different file: detach while the reader is replaced.
                    Unsubscribe(Replay);
                }

                try
                {
                    Replay.Load(path);
                }
                catch
                {
                    // Loading failed — restore the previous wiring rather than leaving nothing
                    // subscribed, then let the caller see the exception.
                    active = Live;
                    Subscribe(Live);
                    throw;
                }

                data = new();
                active = Replay;
                Subscribe(Replay);
            }

            logger.LogInformation("Switched to replay of '{Path}'", path);
            ActiveSourceChanged?.Invoke();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            lock (gate)
            {
                disposed = true;
                Unsubscribe(active);
            }

            DataUpdated = null;
            RawFrameReceived = null;
            StartLightsChanged = null;
            ActiveSourceChanged = null;
        }

        private void Subscribe(ISharedSource source)
        {
            source.DataUpdated += onDataUpdated;
            source.RawFrameReceived += onRawFrameReceived;
            source.StartLightsChanged += onStartLightsChanged;
        }

        private void Unsubscribe(ISharedSource source)
        {
            source.DataUpdated -= onDataUpdated;
            source.RawFrameReceived -= onRawFrameReceived;
            source.StartLightsChanged -= onStartLightsChanged;
        }

        private void ForwardDataUpdated(Shared value)
        {
            data = value;
            DataUpdated?.Invoke(value);
        }

        private void ForwardRawFrame(ReadOnlyMemory<byte> frame)
            => RawFrameReceived?.Invoke(frame);

        private void ForwardStartLights(int startLights)
            => StartLightsChanged?.Invoke(startLights);
    }
}
