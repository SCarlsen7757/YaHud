using System.IO.Compression;
using System.Text;

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
    /// </remarks>
    public sealed class TelemetryRecordingReader : IDisposable
    {
        /// <summary>Bytes of a block header: compressedLen, firstFrameMs, frameCount.</summary>
        private const int BlockHeaderSize = sizeof(int) + sizeof(uint) + sizeof(int);

        /// <summary>Bytes of a record header: elapsedMs, recordType, payloadLen.</summary>
        private const int RecordHeaderSize = sizeof(uint) + 1 + sizeof(int);

        /// <summary>A decoded record's position inside the decompressed block buffer.</summary>
        private readonly record struct BlockRecord(uint ElapsedMs, RecordingRecordType Type, int Offset, int Length);

        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataStart;
        private readonly List<RecordingBlockIndexEntry> index = [];
        private readonly List<RecordingMarker> markers = [];
        private readonly List<int> classIds = [];
        private readonly List<BlockRecord> blockRecords = [];

        private byte[] blockBuffer = [];
        private int loadedBlock = -1;
        private int recordPointer;
        private uint position;
        private bool disposed;

        /// <summary>
        /// Opens a recording and reads its header, index and markers.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <exception cref="InvalidDataException">
        /// The file is not a recording, or was written against a different <c>Shared</c> layout.
        /// </exception>
        public TelemetryRecordingReader(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            Path = path;
            stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 64 * 1024);
            reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            try
            {
                Header = TelemetryRecordingHeader.Read(reader);
            }
            catch (EndOfStreamException ex)
            {
                reader.Dispose();
                stream.Dispose();
                throw new InvalidDataException($"\"{path}\" is truncated before the end of the recording header.", ex);
            }
            catch
            {
                reader.Dispose();
                stream.Dispose();
                throw;
            }

            dataStart = stream.Position;

            // Metadata defaults: the header values, which is what a crashed recording is left with.
            MaxNumCars = Header.NumCarsAtStart;
            if (Header.PlayerClassId != 0)
            {
                classIds.Add(Header.PlayerClassId);
            }

            if (!TryLoadFooter())
            {
                RebuildIndex();
                IndexWasRebuilt = true;
            }

            Index = index;
            Markers = markers;
            ClassIds = classIds;
        }

        /// <summary>Path of the file being read.</summary>
        public string Path { get; }

        /// <summary>The validated file header.</summary>
        public TelemetryRecordingHeader Header { get; }

        /// <summary>Total recorded duration.</summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>Total number of frame records in the file.</summary>
        public long TotalFrames { get; private set; }

        /// <summary>
        /// Highest driver count observed over the whole session, from the footer summary; falls back
        /// to <see cref="TelemetryRecordingHeader.NumCarsAtStart"/> when the footer is missing.
        /// </summary>
        public int MaxNumCars { get; private set; }

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
        public bool IndexWasRebuilt { get; private set; }

        /// <summary>Elapsed time of the record most recently returned by <see cref="ReadNext"/>.</summary>
        public TimeSpan Position => TimeSpan.FromMilliseconds(position);

        /// <summary>
        /// Positions the reader so that the next <see cref="ReadNext"/> returns the first record at
        /// or after <paramref name="position"/>.
        /// </summary>
        /// <param name="position">Target position, clamped to <c>[0, <see cref="Duration"/>]</c>.</param>
        public void SeekTo(TimeSpan position)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            var targetMs = position <= TimeSpan.Zero
                ? 0u
                : (uint)Math.Min(position.TotalMilliseconds, Duration.TotalMilliseconds);

            if (index.Count == 0)
            {
                this.position = targetMs;
                return;
            }

            // Last block whose first record is at or before the target: the target record, if it
            // exists at all, is inside it. At most one block is decoded.
            var block = 0;
            for (var i = index.Count - 1; i >= 0; i--)
            {
                if (index[i].FirstFrameMs <= targetMs)
                {
                    block = i;
                    break;
                }
            }

            LoadBlock(block);

            recordPointer = 0;
            while (recordPointer < blockRecords.Count && blockRecords[recordPointer].ElapsedMs < targetMs)
            {
                recordPointer++;
            }

            this.position = targetMs;
        }

        /// <summary>Positions the reader back at the first record. Equivalent to <c>SeekTo(TimeSpan.Zero)</c>.</summary>
        public void Rewind() => SeekTo(TimeSpan.Zero);

        /// <summary>
        /// Returns the start of the session containing <paramref name="position"/>. Because recording
        /// splits per session, this is always <see cref="TimeSpan.Zero"/> for a well-formed file; it
        /// exists so callers do not have to encode that assumption.
        /// </summary>
        /// <param name="position">A position within the recording.</param>
        /// <returns>The session start time.</returns>
        public TimeSpan SessionStartAt(TimeSpan position) => TimeSpan.Zero;

        /// <summary>
        /// Finds the position at which the given lap starts, from the
        /// <see cref="RecordingMarkerType.LapStart"/> markers.
        /// </summary>
        /// <param name="lap">Completed-lap count to jump to.</param>
        /// <param name="position">Receives the start position of that lap.</param>
        /// <returns><see langword="false"/> when no such marker exists.</returns>
        public bool TryGetLapStart(int lap, out TimeSpan position)
        {
            foreach (var marker in markers)
            {
                if (marker.Type == RecordingMarkerType.LapStart && marker.Value == lap)
                {
                    position = TimeSpan.FromMilliseconds(marker.ElapsedMs);
                    return true;
                }
            }

            position = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Reads the next record. Frame records are returned reconstructed to full
        /// <see cref="FrameTruncation.SizeOfShared"/> size and ready to marshal.
        /// </summary>
        /// <param name="record">Receives the record.</param>
        /// <returns><see langword="false"/> at end of file.</returns>
        public bool ReadNext(out ReplayRecord record)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (loadedBlock < 0)
            {
                if (index.Count == 0)
                {
                    record = default;
                    return false;
                }

                LoadBlock(0);
                recordPointer = 0;
            }

            while (recordPointer >= blockRecords.Count)
            {
                if (loadedBlock + 1 >= index.Count)
                {
                    record = default;
                    return false;
                }

                LoadBlock(loadedBlock + 1);
                recordPointer = 0;
            }

            var entry = blockRecords[recordPointer++];
            position = entry.ElapsedMs;

            var payload = blockBuffer.AsSpan(entry.Offset, entry.Length);
            record = entry.Type switch
            {
                RecordingRecordType.Frame =>
                    new ReplayRecord(RecordingRecordType.Frame, entry.ElapsedMs, FrameTruncation.Restore(payload), 0),
                RecordingRecordType.StartLights =>
                    new ReplayRecord(
                        RecordingRecordType.StartLights,
                        entry.ElapsedMs,
                        null,
                        payload.Length >= sizeof(int) ? BitConverter.ToInt32(payload) : 0),
                _ => new ReplayRecord(entry.Type, entry.ElapsedMs, null, 0),
            };

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            reader.Dispose();
            stream.Dispose();
        }

        /// <summary>
        /// Reads the footer written on a clean close. Returns <see langword="false"/> when the file
        /// was cut short, which sends the caller down the rebuild-by-scanning path.
        /// </summary>
        private bool TryLoadFooter()
        {
            if (Header.IndexOffset <= dataStart || Header.IndexOffset >= stream.Length)
            {
                return DiscardFooterState();
            }

            try
            {
                // The trailing magic is the proof that the footer was written in full.
                stream.Position = stream.Length - TelemetryRecordingHeader.FooterMagic.Length;
                var trailer = reader.ReadBytes(TelemetryRecordingHeader.FooterMagic.Length);
                if (!trailer.AsSpan().SequenceEqual(TelemetryRecordingHeader.FooterMagic))
                {
                    return DiscardFooterState();
                }

                stream.Position = Header.IndexOffset;

                var entryCount = reader.ReadInt32();
                if (entryCount < 0 || entryCount > (stream.Length - Header.IndexOffset) / 4)
                {
                    return DiscardFooterState();
                }

                for (var i = 0; i < entryCount; i++)
                {
                    index.Add(new RecordingBlockIndexEntry(
                        reader.ReadUInt32(), reader.ReadInt64(), reader.ReadInt32()));
                }

                var markerCount = reader.ReadInt32();
                if (markerCount < 0 || markerCount > (stream.Length - stream.Position) / 4)
                {
                    return DiscardFooterState();
                }

                for (var i = 0; i < markerCount; i++)
                {
                    markers.Add(new RecordingMarker(
                        reader.ReadUInt32(), (RecordingMarkerType)reader.ReadByte(), reader.ReadInt32()));
                }

                TotalFrames = reader.ReadInt64();
                Duration = TimeSpan.FromMilliseconds(reader.ReadUInt32());
                MaxNumCars = reader.ReadInt32();

                var classCount = reader.ReadInt32();
                if (classCount < 0 || classCount > (stream.Length - stream.Position) / 4)
                {
                    return DiscardFooterState();
                }

                classIds.Clear();
                for (var i = 0; i < classCount; i++)
                {
                    classIds.Add(reader.ReadInt32());
                }

                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or OverflowException)
            {
                return DiscardFooterState();
            }
        }

        /// <summary>
        /// Drops anything a partially-read footer left behind and restores the header-derived
        /// defaults, then reports failure so the caller rebuilds the index by scanning.
        /// </summary>
        /// <remarks>
        /// Every footer bail routes through here. The bounds checks sit between the index, marker
        /// and class sections, so failing a later one would otherwise leave the earlier sections
        /// populated from a footer already known to be damaged — and <see cref="RebuildIndex"/>
        /// clears the index and markers but not the class list, so those stale entries would
        /// survive into a rebuilt file.
        /// </remarks>
        private bool DiscardFooterState()
        {
            index.Clear();
            markers.Clear();
            classIds.Clear();
            if (Header.PlayerClassId != 0)
            {
                classIds.Add(Header.PlayerClassId);
            }

            MaxNumCars = Header.NumCarsAtStart;
            TotalFrames = 0;
            Duration = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Rebuilds the block index by walking block headers. Each block header carries its own
        /// compressed length, so this is a seek-only scan rather than a decode — a session killed
        /// mid-write is still readable.
        /// </summary>
        private void RebuildIndex()
        {
            index.Clear();
            markers.Clear();

            var offset = dataStart;
            var length = stream.Length;
            long recordCount = 0;

            while (offset + BlockHeaderSize <= length)
            {
                stream.Position = offset;

                int compressedLength;
                uint firstFrameMs;
                int frameCount;
                try
                {
                    compressedLength = reader.ReadInt32();
                    firstFrameMs = reader.ReadUInt32();
                    frameCount = reader.ReadInt32();
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                // A partially written block, or the footer we ran into, ends the walk. Blocks are
                // ordered by elapsed time, so a backwards step means we are no longer reading blocks
                // — which is what the start of an intact footer looks like when only the index-offset
                // patch is missing.
                if (compressedLength <= 0
                    || frameCount <= 0
                    || offset + BlockHeaderSize + compressedLength > length
                    || (index.Count > 0 && firstFrameMs < index[^1].FirstFrameMs))
                {
                    break;
                }

                index.Add(new RecordingBlockIndexEntry(firstFrameMs, offset, frameCount));
                recordCount += frameCount;
                offset += BlockHeaderSize + compressedLength;
            }

            // Duration needs the elapsed time of the last record, which only the block itself holds.
            // Decoding one block is cheap and is what makes a crashed recording report a sensible
            // length instead of the start of its final block. It also confirms the last entry really
            // was a block: anything the scan mistook for one decodes to nothing and is dropped.
            while (index.Count > 0)
            {
                var last = index.Count - 1;
                var decoded = false;
                try
                {
                    LoadBlock(last);
                    decoded = blockRecords.Count > 0;
                }
                catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException)
                {
                    decoded = false;
                }

                if (decoded)
                {
                    Duration = TimeSpan.FromMilliseconds(blockRecords[^1].ElapsedMs);
                    break;
                }

                recordCount -= index[last].FrameCount;
                index.RemoveAt(last);
                loadedBlock = -1;
            }

            TotalFrames = Math.Max(recordCount, 0);
            loadedBlock = -1;
            recordPointer = 0;
            blockRecords.Clear();
        }

        private void LoadBlock(int blockIndex)
        {
            if (loadedBlock == blockIndex)
            {
                return;
            }

            var entry = index[blockIndex];
            stream.Position = entry.FileOffset;

            var compressedLength = reader.ReadInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadInt32();

            var compressed = reader.ReadBytes(compressedLength);
            if (compressed.Length != compressedLength)
            {
                throw new InvalidDataException(
                    $"Block {blockIndex} of \"{Path}\" is truncated at byte {entry.FileOffset}.");
            }

            using var source = new MemoryStream(compressed, writable: false);
            using var brotli = new BrotliStream(source, CompressionMode.Decompress);
            using var decoded = new MemoryStream(compressedLength * 4);
            brotli.CopyTo(decoded);

            blockBuffer = decoded.GetBuffer();
            var decodedLength = (int)decoded.Length;

            blockRecords.Clear();
            var cursor = 0;
            while (cursor + RecordHeaderSize <= decodedLength)
            {
                var elapsedMs = BitConverter.ToUInt32(blockBuffer, cursor);
                var type = (RecordingRecordType)blockBuffer[cursor + sizeof(uint)];
                var payloadLength = BitConverter.ToInt32(blockBuffer, cursor + sizeof(uint) + 1);
                cursor += RecordHeaderSize;

                if (payloadLength < 0 || cursor + payloadLength > decodedLength)
                {
                    break;
                }

                blockRecords.Add(new BlockRecord(elapsedMs, type, cursor, payloadLength));
                cursor += payloadLength;
            }

            loadedBlock = blockIndex;
            recordPointer = 0;
        }
    }
}
