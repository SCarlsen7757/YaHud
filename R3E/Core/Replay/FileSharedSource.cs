using R3E.Core.Interfaces;
using R3E.Core.Recording;
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
    /// <para>Implemented by agent B3.</para>
    /// </remarks>
    public sealed class FileSharedSource : ISharedSource, IDisposable
    {
        /// <inheritdoc />
#pragma warning disable CS0067 // Events are never used - raised by agent B3
        public event Action<Shared>? DataUpdated;

        /// <inheritdoc />
        public event Action<ReadOnlyMemory<byte>>? RawFrameReceived;

        /// <inheritdoc />
        public event Action<int>? StartLightsChanged;
#pragma warning restore CS0067

        /// <inheritdoc />
        public Shared Data { get; } = new();

        /// <summary>Path of the loaded recording, or <see langword="null"/> when nothing is loaded.</summary>
        public string? CurrentFile { get; }

        /// <summary>The underlying reader, or <see langword="null"/> when nothing is loaded.</summary>
        public TelemetryRecordingReader? Reader { get; }

        /// <summary>Elapsed position of the most recently emitted record.</summary>
        public TimeSpan Position { get; }

        /// <summary>Total duration of the loaded recording.</summary>
        public TimeSpan Duration { get; }

        /// <summary><see langword="true"/> once every record has been emitted.</summary>
        public bool IsAtEnd { get; }

        /// <summary>
        /// Opens a recording, replacing anything already loaded.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <exception cref="InvalidDataException">The file was written against a different layout.</exception>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Load(string path)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>Closes the loaded recording and releases the reader.</summary>
        /// <remarks>Implemented by agent B3.</remarks>
        public void Unload()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Emits the next record — marshalling and raising the matching event. This is the pump step
        /// the controller calls; it never sleeps.
        /// </summary>
        /// <returns><see langword="false"/> at end of file.</returns>
        /// <remarks>Implemented by agent B3.</remarks>
        public bool EmitNext()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Returns the elapsed position of the next record without emitting it, so the pump can
        /// decide how long to wait.
        /// </summary>
        /// <param name="position">Receives the position of the next record.</param>
        /// <returns><see langword="false"/> at end of file.</returns>
        /// <remarks>Implemented by agent B3.</remarks>
        public bool TryPeekNext(out TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Repositions the reader without emitting anything. Used by the controller's backward-seek
        /// path, which rewinds to the start and then catches up forward.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void SeekTo(TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B3.</remarks>
        public void Dispose()
            => throw new NotImplementedException("Implemented by agent B3");
    }
}
