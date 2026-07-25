using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Interfaces;
using R3E.Core.Recording;
using R3E.Core.SharedMemory;
using R3E.Data;

namespace R3E.Core.Replay
{
    /// <summary>
    /// An <see cref="ISharedSource"/> backed by a recording file rather than by the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wraps a <see cref="TelemetryRecordingReader"/>, reconstructs each frame to full size, marshals
    /// it with the same <c>SharedMarshaller</c> the live sources use, and raises
    /// <see cref="DataUpdated"/>. Everything above <see cref="ISharedSource"/> is unchanged and
    /// unaware it is being replayed.
    /// </para>
    /// <para>
    /// This type does not own pacing policy — <see cref="ReplayController"/> drives it, either at
    /// real-time cadence or as fast as the pipeline can consume during a catch-up.
    /// </para>
    /// <para>
    /// Threading: every member except <see cref="Load"/> and <see cref="Unload"/> is called from the
    /// controller's pump thread only. The controller stops the pump before loading or unloading, so
    /// this type needs no internal locking on the hot path.
    /// </para>
    /// </remarks>
    public sealed class FileSharedSource : ISharedSource, IDisposable
    {
        private readonly ILogger<FileSharedSource> logger;

        private TelemetryRecordingReader? reader;
        private Shared data;

        /// <summary>
        /// One-record lookahead. <see cref="TelemetryRecordingReader"/> exposes no peek, so the
        /// record is read eagerly and held until <see cref="EmitNext"/> consumes it. Invalidated by
        /// every reposition.
        /// </summary>
        private ReplayRecord pending;
        private bool hasPending;
        private bool exhausted;

        private TimeSpan position;
        private bool disposed;

        /// <summary>Creates the source with nothing loaded.</summary>
        /// <param name="logger">Optional logger.</param>
        public FileSharedSource(ILogger<FileSharedSource>? logger = null)
        {
            this.logger = logger ?? NullLogger<FileSharedSource>.Instance;
            data = new();
        }

        /// <inheritdoc />
        public event Action<Shared>? DataUpdated;

        /// <inheritdoc />
        public event Action<ReadOnlyMemory<byte>>? RawFrameReceived;

        /// <inheritdoc />
        public event Action<int>? StartLightsChanged;

        /// <inheritdoc />
        public Shared Data => data;

        /// <summary>Path of the loaded recording, or <see langword="null"/> when nothing is loaded.</summary>
        public string? CurrentFile { get; private set; }

        /// <summary>The underlying reader, or <see langword="null"/> when nothing is loaded.</summary>
        public TelemetryRecordingReader? Reader => reader;

        /// <summary>Elapsed position of the most recently emitted record.</summary>
        public TimeSpan Position => position;

        /// <summary>Total duration of the loaded recording.</summary>
        public TimeSpan Duration => reader?.Duration ?? TimeSpan.Zero;

        /// <summary><see langword="true"/> once every record has been emitted.</summary>
        public bool IsAtEnd => reader is null || (exhausted && !hasPending);

        /// <summary>
        /// Opens a recording, replacing anything already loaded.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <exception cref="InvalidDataException">The file was written against a different layout.</exception>
        public void Load(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            Unload();

            reader = new TelemetryRecordingReader(path);
            CurrentFile = path;
            data = new();
            position = TimeSpan.Zero;
            hasPending = false;
            exhausted = false;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Loaded recording '{Path}' ({Duration}, {Frames} frames{Rebuilt})",
                    path, reader.Duration, reader.TotalFrames,
                    reader.IndexWasRebuilt ? ", index rebuilt" : string.Empty);
            }
        }

        /// <summary>Closes the loaded recording and releases the reader.</summary>
        public void Unload()
        {
            if (reader is null)
            {
                return;
            }

            try
            {
                reader.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error disposing recording reader for '{Path}'", CurrentFile);
            }

            reader = null;
            CurrentFile = null;
            data = new();
            position = TimeSpan.Zero;
            hasPending = false;
            exhausted = false;
        }

        /// <summary>
        /// Emits the next record — marshalling and raising the matching event. This is the pump step
        /// the controller calls; it never sleeps.
        /// </summary>
        /// <returns><see langword="false"/> at end of file.</returns>
        public bool EmitNext()
        {
            if (!EnsurePending())
            {
                return false;
            }

            var record = pending;
            hasPending = false;
            pending = default;

            position = TimeSpan.FromMilliseconds(record.ElapsedMs);

            switch (record.Type)
            {
                case RecordingRecordType.Frame:
                    EmitFrame(record.Frame);
                    break;

                case RecordingRecordType.StartLights:
                    StartLightsChanged?.Invoke(record.StartLights);
                    break;

                default:
                    // Unknown record types are skipped rather than fatal, so a newer recording still
                    // replays the parts this build understands.
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Skipping unknown record type {Type}", record.Type);
                    }

                    break;
            }

            return true;
        }

        /// <summary>
        /// Returns the elapsed position of the next record without emitting it, so the pump can
        /// decide how long to wait.
        /// </summary>
        /// <param name="position">Receives the position of the next record.</param>
        /// <returns><see langword="false"/> at end of file.</returns>
        public bool TryPeekNext(out TimeSpan position)
        {
            if (!EnsurePending())
            {
                position = default;
                return false;
            }

            position = TimeSpan.FromMilliseconds(pending.ElapsedMs);
            return true;
        }

        /// <summary>
        /// Repositions the reader without emitting anything. Used by the controller's backward-seek
        /// path, which rewinds to the start and then catches up forward.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        /// <remarks>
        /// The controller only ever calls this with <see cref="TimeSpan.Zero"/>. Repositioning
        /// anywhere else would put a forward jump into the stream that consumers above
        /// <see cref="ISharedSource"/> are deliberately not built to survive.
        /// </remarks>
        public void SeekTo(TimeSpan position)
        {
            if (reader is null)
            {
                return;
            }

            var clamped = position;
            if (clamped < TimeSpan.Zero)
            {
                clamped = TimeSpan.Zero;
            }
            else if (clamped > Duration)
            {
                clamped = Duration;
            }

            hasPending = false;
            pending = default;
            exhausted = false;

            if (clamped == TimeSpan.Zero)
            {
                reader.Rewind();
            }
            else
            {
                reader.SeekTo(clamped);
            }

            this.position = clamped;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unload();
            DataUpdated = null;
            RawFrameReceived = null;
            StartLightsChanged = null;
        }

        private void EmitFrame(byte[]? frame)
        {
            if (frame is null)
            {
                return;
            }

            // Raw first, mirroring the live sources, so a throwing downstream handler cannot cost a
            // raw subscriber the frame.
            RawFrameReceived?.Invoke(frame);

            if (SharedMarshaller.TryMarshalShared(frame, out var marshalled))
            {
                data = marshalled;
                DataUpdated?.Invoke(data);
            }
            else
            {
                logger.LogWarning("Failed to marshal a replayed frame at {Position}", position);
            }
        }

        private bool EnsurePending()
        {
            if (hasPending)
            {
                return true;
            }

            if (reader is null || exhausted)
            {
                return false;
            }

            if (!reader.ReadNext(out var record))
            {
                exhausted = true;
                return false;
            }

            pending = record;
            hasPending = true;
            return true;
        }
    }
}
