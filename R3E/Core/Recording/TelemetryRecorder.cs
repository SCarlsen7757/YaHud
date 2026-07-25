using R3E.Core.Interfaces;

namespace R3E.Core.Recording
{
    /// <summary>
    /// Subscribes to <see cref="ISharedSource.RawFrameReceived"/> and persists raw telemetry to
    /// <c>.yhtl</c> files, one file per session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ingest loop must never wait on disk. <c>RawFrameReceived</c> fires synchronously on the
    /// polling thread, so a disk hiccup, AV scan or full buffer would otherwise stall telemetry and
    /// visibly freeze the HUD. The write path is therefore: truncate into an <c>ArrayPool</c> buffer,
    /// push onto a bounded channel with <c>DropWrite</c>, and let a background writer task compress
    /// and write. For a debug recorder, dropping frames beats stuttering the HUD, and every frame is
    /// independently decodable, so a gap only shows as a longer inter-frame delta on replay.
    /// </para>
    /// <para>
    /// One recording run produces one file per session. The recorder watches <c>SessionType</c> and
    /// <c>TrackId</c> straight out of the raw buffer — no marshalling — and on a change finalises the
    /// current file and opens a new one on the next frame. Files live in a per-run folder so the
    /// sessions of a stint stay together.
    /// </para>
    /// <para>Implemented by agent B2.</para>
    /// </remarks>
    public sealed class TelemetryRecorder : IHostedService, IAsyncDisposable
    {
        /// <summary>
        /// Creates the recorder. It stays inert until <see cref="Start"/> is called, or immediately
        /// starts when <see cref="RecordingOptions.AutoStart"/> is set.
        /// </summary>
        /// <param name="sharedSource">The source whose raw frames are captured.</param>
        /// <param name="options">Recording settings from configuration and launch arguments.</param>
        /// <param name="logger">Optional logger.</param>
        /// <remarks>Implemented by agent B2.</remarks>
        public TelemetryRecorder(ISharedSource sharedSource, RecordingOptions options, ILogger<TelemetryRecorder>? logger = null)
            => throw new NotImplementedException("Implemented by agent B2");

        /// <summary>Raised whenever any of the state below changes, so the UI can re-render.</summary>
#pragma warning disable CS0067 // Event is never used - raised by agent B2
        public event Action? StateChanged;
#pragma warning restore CS0067

        /// <summary><see langword="true"/> while frames are being captured.</summary>
        public bool IsRecording { get; }

        /// <summary>
        /// Path of the file currently being written, or <see langword="null"/> when not recording or
        /// when no frame has arrived yet — the file is created on the first frame.
        /// </summary>
        public string? CurrentFile { get; }

        /// <summary>Time since recording started.</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>Frames written across this recording run.</summary>
        public long FrameCount { get; }

        /// <summary>Bytes written across this recording run.</summary>
        public long BytesWritten { get; }

        /// <summary>
        /// Frames dropped because the bounded channel was full. Non-zero means the disk could not
        /// keep up; the HUD was protected rather than stalled.
        /// </summary>
        public long DroppedFrames { get; }

        /// <summary>Folder of the current recording run, containing this run's session files.</summary>
        public string? CurrentRunFolder { get; }

        /// <summary>
        /// Starts recording. Nothing is written until the first frame arrives.
        /// </summary>
        /// <param name="path">
        /// Optional run folder. When null, a timestamped folder is created under the configured
        /// recordings directory.
        /// </param>
        /// <remarks>Implemented by agent B2.</remarks>
        public void Start(string? path = null)
            => throw new NotImplementedException("Implemented by agent B2");

        /// <summary>
        /// Stops recording and finalises the current file — footer, summary and index patch.
        /// Idempotent.
        /// </summary>
        /// <remarks>Implemented by agent B2.</remarks>
        public void Stop()
            => throw new NotImplementedException("Implemented by agent B2");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B2.</remarks>
        public Task StartAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException("Implemented by agent B2");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B2.</remarks>
        public Task StopAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException("Implemented by agent B2");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B2.</remarks>
        public ValueTask DisposeAsync()
            => throw new NotImplementedException("Implemented by agent B2");
    }
}
