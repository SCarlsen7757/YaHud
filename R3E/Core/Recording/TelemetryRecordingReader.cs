namespace R3E.Core.Recording
{
    /// <summary>
    /// Reads a <c>.yhtl</c> recording back: validates the header against this build's
    /// <c>Shared</c> layout, loads (or rebuilds) the block index, and streams records forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reader prefers the footer summary and falls back to the header values when the footer is
    /// missing — after a crash, metadata is still available, just as-of-first-frame. When
    /// <see cref="TelemetryRecordingHeader.IndexOffset"/> is zero or the trailing magic is absent,
    /// the index is rebuilt by walking block headers, which is a seek-only scan rather than a decode.
    /// </para>
    /// <para>
    /// Reading is strictly forward. A backward seek is expressed by seeking to an earlier position,
    /// which the reader satisfies by restarting from the block that contains it — replay's rewind
    /// semantics live in <c>ReplayController</c>, not here.
    /// </para>
    /// <para>Implemented by agent B1.</para>
    /// </remarks>
    public sealed class TelemetryRecordingReader : IDisposable
    {
        /// <summary>
        /// Opens a recording and reads its header, index and markers.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <exception cref="InvalidDataException">
        /// The file is not a recording, or was written against a different <c>Shared</c> layout.
        /// </exception>
        /// <remarks>Implemented by agent B1.</remarks>
        public TelemetryRecordingReader(string path)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>Path of the file being read.</summary>
        public string Path { get; } = string.Empty;

        /// <summary>The validated file header.</summary>
        public TelemetryRecordingHeader Header { get; } = new();

        /// <summary>Total recorded duration.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Total number of frame records in the file.</summary>
        public long TotalFrames { get; }

        /// <summary>
        /// Highest driver count observed over the whole session, from the footer summary; falls back
        /// to <see cref="TelemetryRecordingHeader.NumCarsAtStart"/> when the footer is missing.
        /// </summary>
        public int MaxNumCars { get; }

        /// <summary>Every car class observed in the session, from the footer summary.</summary>
        public IReadOnlyList<int> ClassIds { get; } = [];

        /// <summary>All timeline markers, ordered by elapsed time.</summary>
        public IReadOnlyList<RecordingMarker> Markers { get; } = [];

        /// <summary>The block index, ordered by elapsed time.</summary>
        public IReadOnlyList<RecordingBlockIndexEntry> Index { get; } = [];

        /// <summary>
        /// <see langword="true"/> when the footer was missing or damaged and the index had to be
        /// rebuilt by scanning — i.e. the recording was cut short by a crash.
        /// </summary>
        public bool IndexWasRebuilt { get; }

        /// <summary>Elapsed time of the record most recently returned by <see cref="ReadNext"/>.</summary>
        public TimeSpan Position { get; }

        /// <summary>
        /// Positions the reader so that the next <see cref="ReadNext"/> returns the first record at
        /// or after <paramref name="position"/>.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void SeekTo(TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>Positions the reader back at the first record. Equivalent to <c>SeekTo(TimeSpan.Zero)</c>.</summary>
        /// <remarks>Implemented by agent B1.</remarks>
        public void Rewind()
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Returns the start of the session containing <paramref name="position"/>. Because recording
        /// splits per session, this is always <see cref="TimeSpan.Zero"/> for a well-formed file; it
        /// exists so callers do not have to encode that assumption.
        /// </summary>
        /// <param name="position">A position within the recording.</param>
        /// <returns>The session start time.</returns>
        /// <remarks>Implemented by agent B1.</remarks>
        public TimeSpan SessionStartAt(TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Finds the position at which the given lap starts, from the
        /// <see cref="RecordingMarkerType.LapStart"/> markers.
        /// </summary>
        /// <param name="lap">Completed-lap count to jump to.</param>
        /// <param name="position">Receives the start position of that lap.</param>
        /// <returns><see langword="false"/> when no such marker exists.</returns>
        /// <remarks>Implemented by agent B1.</remarks>
        public bool TryGetLapStart(int lap, out TimeSpan position)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Reads the next record. Frame records are returned reconstructed to full
        /// <see cref="FrameTruncation.SizeOfShared"/> size and ready to marshal.
        /// </summary>
        /// <param name="record">Receives the record.</param>
        /// <returns><see langword="false"/> at end of file.</returns>
        /// <remarks>Implemented by agent B1.</remarks>
        public bool ReadNext(out ReplayRecord record)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B1.</remarks>
        public void Dispose()
            => throw new NotImplementedException("Implemented by agent B1");
    }
}
