using System.IO.Compression;
using System.Text;

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
    /// </remarks>
    public sealed class TelemetryRecordingWriter : IDisposable, IAsyncDisposable
    {
        /// <summary>Default compression block duration, in seconds.</summary>
        public const double DefaultBlockSeconds = 3d;

        /// <summary>Smallest accepted block duration, in seconds.</summary>
        private const double MinBlockSeconds = 0.1d;

        /// <summary>Largest accepted block duration, in seconds.</summary>
        private const double MaxBlockSeconds = 600d;

        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly MemoryStream block = new();
        private readonly List<RecordingBlockIndexEntry> index = [];
        private readonly List<RecordingMarker> markers = [];
        private readonly SortedSet<int> classIds = [];
        private readonly uint blockSpanMs;

        private bool blockOpen;
        private uint blockFirstMs;
        private int blockRecordCount;
        private long frameCount;
        private uint durationMs;
        private int maxNumCars;
        private bool closed;
        private bool disposed;

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
        public TelemetryRecordingWriter(string path, TelemetryRecordingHeader header, double blockSeconds = DefaultBlockSeconds)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(header);

            if (double.IsNaN(blockSeconds) || blockSeconds < MinBlockSeconds || blockSeconds > MaxBlockSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blockSeconds),
                    blockSeconds,
                    $"Block duration must be between {MinBlockSeconds} and {MaxBlockSeconds} seconds.");
            }

            Path = path;
            Header = header;
            BlockSeconds = blockSeconds;
            blockSpanMs = (uint)Math.Max(1d, Math.Round(blockSeconds * 1000d));

            var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // The index offset is patched in place on a clean close, so the stream must stay seekable
            // and readers must be able to open the file while it is still being written.
            stream = new FileStream(
                path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, bufferSize: 64 * 1024);
            writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            header.IndexOffset = 0;
            header.Write(writer);
            writer.Flush();

            if (header.PlayerClassId != 0)
            {
                classIds.Add(header.PlayerClassId);
            }

            maxNumCars = header.NumCarsAtStart;
        }

        /// <summary>Path of the file being written.</summary>
        public string Path { get; }

        /// <summary>Header written at the start of the file.</summary>
        public TelemetryRecordingHeader Header { get; }

        /// <summary>Compression block duration, in seconds.</summary>
        public double BlockSeconds { get; }

        /// <summary>Number of frame records written so far.</summary>
        public long FrameCount => frameCount;

        /// <summary>Bytes flushed to disk so far.</summary>
        public long BytesWritten => stream.Length;

        /// <summary>Elapsed time of the most recent record written.</summary>
        public uint DurationMs => durationMs;

        /// <summary>Highest driver count observed so far; goes into the footer summary.</summary>
        public int MaxNumCars => maxNumCars;

        /// <summary><see langword="true"/> once the footer has been written and the file closed.</summary>
        public bool IsClosed => closed;

        /// <summary>
        /// Appends a truncated frame.
        /// </summary>
        /// <param name="truncated">Truncated frame bytes, as produced by <see cref="FrameTruncation.Truncate"/>.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        public void WriteFrame(ReadOnlySpan<byte> truncated, uint elapsedMs)
        {
            if (truncated.Length < FrameTruncation.MinTruncatedLength
                || truncated.Length > FrameTruncation.SizeOfShared)
            {
                throw new ArgumentException(
                    $"Truncated frame length {truncated.Length} is outside " +
                    $"[{FrameTruncation.MinTruncatedLength}, {FrameTruncation.SizeOfShared}].",
                    nameof(truncated));
            }

            Append(RecordingRecordType.Frame, truncated, elapsedMs);
            frameCount++;

            var numCars = FrameTruncation.ReadNumCars(truncated);
            if (numCars > maxNumCars)
            {
                maxNumCars = numCars;
            }
        }

        /// <summary>
        /// Appends a start-light sample. Only raised on Windows; recordings captured on Linux contain
        /// no start-light records.
        /// </summary>
        /// <param name="value">The new start-light count.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        public void WriteStartLights(int value, uint elapsedMs)
        {
            Span<byte> payload = stackalloc byte[sizeof(int)];
            BitConverter.TryWriteBytes(payload, value);
            Append(RecordingRecordType.StartLights, payload, elapsedMs);
        }

        /// <summary>
        /// Records a timeline marker in the footer. Markers make "jump to lap N" cheap.
        /// </summary>
        /// <param name="type">What the marker denotes.</param>
        /// <param name="value">Marker-specific payload.</param>
        /// <param name="elapsedMs">Milliseconds since the first frame of this file.</param>
        public void AddMarker(RecordingMarkerType type, int value, uint elapsedMs)
        {
            ThrowIfClosed();
            markers.Add(new RecordingMarker(elapsedMs, type, value));
        }

        /// <summary>
        /// Notes a car class observed in this session. The footer carries the full observed set,
        /// because a multi-class field is not knowable from the first frame alone.
        /// </summary>
        /// <param name="classId">The class identifier.</param>
        public void ObserveClass(int classId)
        {
            ThrowIfClosed();
            classIds.Add(classId);
        }

        /// <summary>
        /// Flushes the pending block, writes the footer and summary, and patches
        /// <see cref="TelemetryRecordingHeader.IndexOffset"/>. Idempotent.
        /// </summary>
        public void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;

            FlushBlock();

            var indexOffset = stream.Position;

            writer.Write(index.Count);
            foreach (var entry in index)
            {
                writer.Write(entry.FirstFrameMs);
                writer.Write(entry.FileOffset);
                writer.Write(entry.FrameCount);
            }

            markers.Sort(static (a, b) => a.ElapsedMs.CompareTo(b.ElapsedMs));
            writer.Write(markers.Count);
            foreach (var marker in markers)
            {
                writer.Write(marker.ElapsedMs);
                writer.Write((byte)marker.Type);
                writer.Write(marker.Value);
            }

            writer.Write(frameCount);
            writer.Write(durationMs);
            writer.Write(maxNumCars);
            writer.Write(classIds.Count);
            foreach (var classId in classIds)
            {
                writer.Write(classId);
            }

            writer.Write(TelemetryRecordingHeader.FooterMagic);
            writer.Flush();

            // Patch the header last: until this lands the file reads as "crashed" and the reader
            // rebuilds the index by scanning, which is exactly the behaviour we want mid-write.
            stream.Position = Header.IndexOffsetPosition;
            writer.Write(indexOffset);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            Header.IndexOffset = indexOffset;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            try
            {
                Close();
            }
            finally
            {
                writer.Dispose();
                stream.Dispose();
                block.Dispose();
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void Append(RecordingRecordType type, ReadOnlySpan<byte> payload, uint elapsedMs)
        {
            ThrowIfClosed();

            if (!blockOpen)
            {
                blockFirstMs = elapsedMs;
                blockOpen = true;
            }
            else if (elapsedMs - blockFirstMs >= blockSpanMs)
            {
                FlushBlock();
                blockFirstMs = elapsedMs;
                blockOpen = true;
            }

            Span<byte> prefix = stackalloc byte[sizeof(uint) + 1 + sizeof(int)];
            BitConverter.TryWriteBytes(prefix, elapsedMs);
            prefix[sizeof(uint)] = (byte)type;
            BitConverter.TryWriteBytes(prefix[(sizeof(uint) + 1)..], payload.Length);

            block.Write(prefix);
            block.Write(payload);
            blockRecordCount++;

            if (elapsedMs > durationMs)
            {
                durationMs = elapsedMs;
            }
        }

        private void FlushBlock()
        {
            if (!blockOpen || blockRecordCount == 0)
            {
                blockOpen = false;
                block.SetLength(0);
                blockRecordCount = 0;
                return;
            }

            using var compressed = new MemoryStream();
            using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(block.GetBuffer(), 0, (int)block.Length);
            }

            var offset = stream.Position;
            writer.Write((int)compressed.Length);
            writer.Write(blockFirstMs);
            writer.Write(blockRecordCount);
            writer.Write(compressed.GetBuffer(), 0, (int)compressed.Length);
            writer.Flush();

            index.Add(new RecordingBlockIndexEntry(blockFirstMs, offset, blockRecordCount));

            block.SetLength(0);
            blockRecordCount = 0;
            blockOpen = false;
        }

        private void ThrowIfClosed()
        {
            if (closed)
            {
                throw new InvalidOperationException($"Recording \"{Path}\" has already been closed.");
            }
        }
    }
}
