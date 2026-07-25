namespace R3E.Core.Recording
{
    /// <summary>
    /// Writes a single <c>.yhtl</c> recording: header, Brotli-compressed blocks, and a footer
    /// carrying the block index, the markers and the final summary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One writer instance owns exactly one file, which corresponds to exactly one session — the
    /// recorder finalises and replaces the writer when the session changes, so file start is always
    /// session start.
    /// </para>
    /// <para>
    /// The writer is constructed with a fully populated header because the file is created on the
    /// first frame: the caller marshals that one frame, builds the header from it, and hands both to
    /// the writer.
    /// </para>
    /// <para>
    /// Records accumulate into an in-memory block; a block is flushed once it spans
    /// <see cref="BlockSeconds"/>. Disposing flushes the pending block, appends the footer and
    /// patches <c>indexOffset</c> in the header.
    /// </para>
    /// <para>Implemented by agent B1.</para>
    /// </remarks>
    public sealed class TelemetryRecordingWriter : IDisposable, IAsyncDisposable
    {
        /// <summary>Default compression block duration, in seconds.</summary>
        public const double DefaultBlockSeconds = 3d;

        /// <summary>
        /// Creates the file and writes the header.
        /// </summary>
        /// <param name="path">Destination path; the containing folder is created if needed.</param>
        /// <param name="header">Header built from the first frame of this session.</param>
        /// <param name="blockSeconds">
        /// Compression block duration. Smaller blocks give finer seek granularity and a larger index
        /// but a worse compression ratio; larger blocks compress better but waste more decode per
        /// seek.
        /// </param>
        /// <remarks>Implemented by agent B1.</remarks>
        public TelemetryRecordingWriter(string path, TelemetryRecordingHeader header, double blockSeconds = DefaultBlockSeconds)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>Path of the file being written.</summary>
        public string Path { get; } = string.Empty;

        /// <summary>Header written at the start of the file.</summary>
        public TelemetryRecordingHeader Header { get; } = new();

        /// <summary>Compression block duration, in seconds.</summary>
        public double BlockSeconds { get; }

        /// <summary>Number of frame records written so far.</summary>
        public long FrameCount { get; }

        /// <summary>Bytes flushed to disk so far.</summary>
        public long BytesWritten { get; }

        /// <summary>Elapsed time of the most recent record written.</summary>
        public uint DurationMs { get; }

        /// <summary>Highest driver count observed so far; goes into the footer summary.</summary>
        public int MaxNumCars { get; }

        /// <summary><see langword="true"/> once the footer has been written and the file closed.</summary>
        public bool IsClosed { get; }

        /// <summary>
        /// Appends a truncated frame.
        /// </summary>
        /// <param name="truncated">Truncated frame bytes, as produced by <see cref="FrameTruncation.Truncate"/>.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void WriteFrame(ReadOnlySpan<byte> truncated, uint elapsedMs)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Appends a start-light sample. Only raised on Windows; recordings captured on Linux contain
        /// no start-light records.
        /// </summary>
        /// <param name="value">The new start-light count.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void WriteStartLights(int value, uint elapsedMs)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Records a timeline marker in the footer. Markers make "jump to lap N" cheap.
        /// </summary>
        /// <param name="type">What the marker denotes.</param>
        /// <param name="value">Marker-specific payload.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void AddMarker(RecordingMarkerType type, int value, uint elapsedMs)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Notes a car class observed in this session. The footer carries the full observed set,
        /// because a multi-class field is not knowable from the first frame alone.
        /// </summary>
        /// <param name="classId">The class identifier.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void ObserveClass(int classId)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Flushes the pending block, writes the footer and summary, and patches
        /// <see cref="TelemetryRecordingHeader.IndexOffset"/>. Idempotent.
        /// </summary>
        /// <remarks>Implemented by agent B1.</remarks>
        public void Close()
            => throw new NotImplementedException("Implemented by agent B1");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B1.</remarks>
        public void Dispose()
            => throw new NotImplementedException("Implemented by agent B1");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B1.</remarks>
        public ValueTask DisposeAsync()
            => throw new NotImplementedException("Implemented by agent B1");
    }
}
