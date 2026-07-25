using R3E.Core.Recording;

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
    /// <para>Implemented by agent B3.</para>
    /// </remarks>
    public sealed class ReplayController : IDisposable
    {
        /// <summary>
        /// Catch-ups estimated longer than this suppress widget rendering, so a long rewind does not
        /// flood the Blazor dispatcher. Shorter scrubs keep rendering, which reads as fast-forward.
        /// </summary>
        public static readonly TimeSpan RenderSuppressionThreshold = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="sourceSwitch">The switch used to activate replay and return to live.</param>
        /// <param name="logger">Optional logger.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public ReplayController(SharedSourceSwitch sourceSwitch, ILogger<ReplayController>? logger = null)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Raised whenever transport state, position or catch-up progress changes, so the UI can
        /// re-render.
        /// </summary>
#pragma warning disable CS0067 // Event is never used - raised by agent B3
        public event Action? StateChanged;
#pragma warning restore CS0067

        /// <summary><see langword="true"/> when a recording is loaded and replay is active.</summary>
        public bool IsLoaded { get; }

        /// <summary><see langword="true"/> while the transport is running.</summary>
        public bool IsPlaying { get; }

        /// <summary>Path of the loaded recording, or <see langword="null"/>.</summary>
        public string? CurrentFile { get; }

        /// <summary>Header of the loaded recording, or <see langword="null"/>.</summary>
        public TelemetryRecordingHeader? Header { get; }

        /// <summary>Playback rate multiplier, clamped to <c>[0.1, 8]</c>.</summary>
        public double Speed { get; set; } = 1d;

        /// <summary>Current playback position.</summary>
        public TimeSpan Position { get; }

        /// <summary>Position the pump is moving toward, clamped to <c>[0, <see cref="Duration"/>]</c>.</summary>
        public TimeSpan TargetPosition { get; }

        /// <summary>Total duration of the loaded recording.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Frames emitted since the recording was loaded or last reset.</summary>
        public long FrameIndex { get; }

        /// <summary><see langword="true"/> while the pump is consuming frames faster than real time to reach a target.</summary>
        public bool IsCatchingUp { get; }

        /// <summary>Progress of the running catch-up, <c>0</c>–<c>1</c>.</summary>
        public double CatchUpProgress { get; }

        /// <summary>
        /// <see langword="true"/> while widgets should skip rendering, set during a catch-up
        /// estimated longer than <see cref="RenderSuppressionThreshold"/>.
        /// </summary>
        public bool SuppressRendering { get; }

        /// <summary>Lap-start markers of the loaded recording, for the jump-to-lap control.</summary>
        public IReadOnlyList<RecordingMarker> LapMarkers { get; } = [];

        /// <summary>
        /// Loads a recording and activates replay mode, positioned at the start and paused.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Load(string path)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>Unloads the recording and returns the switch to the live source.</summary>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Unload()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>Starts or resumes playback.</summary>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Play()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>Pauses playback, holding the last frame.</summary>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Pause()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Sets the pump target. Supersedes any in-flight catch-up; play/pause state is preserved.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Seek(TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Advances a fixed number of frames while paused.
        /// </summary>
        /// <param name="frames">Frames to advance; must be positive.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Step(int frames = 1)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Seeks to the start of a lap, using the recording's <c>LapStart</c> markers.
        /// </summary>
        /// <param name="lap">Completed-lap count to jump to.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void JumpToLap(int lap)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B3.</remarks>
        public void Dispose()
            => throw new NotImplementedException("Implemented by agent B3");
    }
}
