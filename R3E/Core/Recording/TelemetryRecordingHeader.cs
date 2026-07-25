using R3E.Data;
using System.Runtime.InteropServices;
using System.Text;

namespace R3E.Core.Recording
{
    /// <summary>The kind of record stored inside a recording block.</summary>
    public enum RecordingRecordType : byte
    {
        /// <summary>A truncated <see cref="Shared"/> frame.</summary>
        Frame = 0,

        /// <summary>A start-light count sampled by the 400 Hz poller.</summary>
        StartLights = 1,
    }

    /// <summary>The kind of timeline marker stored in a recording footer.</summary>
    public enum RecordingMarkerType : byte
    {
        /// <summary>Session phase changed; value is the new <c>Constant.SessionPhase</c>.</summary>
        PhaseChange = 0,

        /// <summary>A new lap started; value is the completed-lap count at that point.</summary>
        LapStart = 1,
    }

    /// <summary>A timeline marker, used for "jump to lap N" without decoding the file.</summary>
    /// <param name="ElapsedMs">Milliseconds from the start of the recording.</param>
    /// <param name="Type">What the marker denotes.</param>
    /// <param name="Value">Marker-specific payload.</param>
    public readonly record struct RecordingMarker(uint ElapsedMs, RecordingMarkerType Type, int Value);

    /// <summary>One entry of the footer block index.</summary>
    /// <param name="FirstFrameMs">Elapsed time of the first record in the block.</param>
    /// <param name="FileOffset">Absolute byte offset of the block header in the file.</param>
    /// <param name="FrameCount">Number of records in the block.</param>
    public readonly record struct RecordingBlockIndexEntry(uint FirstFrameMs, long FileOffset, int FrameCount);

    /// <summary>
    /// A single record read back from a recording.
    /// </summary>
    /// <param name="Type">Which member is meaningful.</param>
    /// <param name="ElapsedMs">Milliseconds from the start of the recording.</param>
    /// <param name="Frame">
    /// For <see cref="RecordingRecordType.Frame"/>, a full-size reconstructed raw frame of
    /// <see cref="FrameTruncation.SizeOfShared"/> bytes. Otherwise <see langword="null"/>.
    /// </param>
    /// <param name="StartLights">For <see cref="RecordingRecordType.StartLights"/>, the light count.</param>
    public readonly record struct ReplayRecord(
        RecordingRecordType Type,
        uint ElapsedMs,
        byte[]? Frame,
        int StartLights);

    /// <summary>
    /// The fixed-size header of a <c>.yhtl</c> recording: format identity, the <see cref="Shared"/>
    /// layout the file was captured against, and the session metadata taken from the first frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is created on the <em>first frame</em>, not on <c>Start()</c>. That is what lets the
    /// header be both complete and fixed-size — track, car and driver information only exist once a
    /// frame has arrived.
    /// </para>
    /// <para>
    /// <see cref="SizeOfShared"/> is the version guard. <see cref="Shared"/> mirrors a layout owned by
    /// Sector3; when a field is added the size changes and an old recording would otherwise silently
    /// misparse into plausible-looking garbage. Readers must refuse loudly on a mismatch.
    /// </para>
    /// <para>Implemented by agent B1.</para>
    /// </remarks>
    public sealed class TelemetryRecordingHeader
    {
        /// <summary>File extension used for recordings.</summary>
        public const string FileExtension = ".yhtl";

        /// <summary>Leading magic, ASCII "YHTL".</summary>
        public static ReadOnlySpan<byte> Magic => "YHTL"u8;

        /// <summary>Trailing magic that terminates a cleanly closed footer, ASCII "YHTX".</summary>
        public static ReadOnlySpan<byte> FooterMagic => "YHTX"u8;

        /// <summary>Format version written by this build.</summary>
        public const ushort CurrentFormatVersion = 1;

        /// <summary>Format version the file was written with.</summary>
        public ushort FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>Reserved format flags (e.g. truncation or compression disabled).</summary>
        public ushort Flags { get; set; }

        /// <summary>Marshalled size of <see cref="Shared"/> when the file was written. Version guard.</summary>
        public int SizeOfShared { get; set; }

        /// <summary>Offset of <c>NumCars</c> within <see cref="Shared"/> when the file was written.</summary>
        public int AllDriversOffset { get; set; }

        /// <summary>Marshalled size of one <c>DriverData</c> when the file was written.</summary>
        public int DriverDataSize { get; set; }

        /// <summary>RaceRoom shared-memory major version, from the first frame.</summary>
        public int R3EVersionMajor { get; set; }

        /// <summary>RaceRoom shared-memory minor version, from the first frame.</summary>
        public int R3EVersionMinor { get; set; }

        /// <summary>YaHud version that produced the recording.</summary>
        public string YaHudVersion { get; set; } = string.Empty;

        /// <summary>UTC ticks at which the first frame was captured.</summary>
        public long StartUtcTicks { get; set; }

        /// <summary>
        /// Absolute byte offset of the footer index, patched in on a clean close. Zero means the
        /// application crashed and the reader must rebuild the index by scanning block headers.
        /// </summary>
        public long IndexOffset { get; set; }

        /// <summary>Session type at the first frame (<c>Constant.Session</c>).</summary>
        public int SessionType { get; set; }

        /// <summary>Session phase at the first frame (<c>Constant.SessionPhase</c>).</summary>
        public int SessionPhaseAtStart { get; set; }

        /// <summary>Track identifier.</summary>
        public int TrackId { get; set; }

        /// <summary>Layout identifier.</summary>
        public int LayoutId { get; set; }

        /// <summary>Track name, decoded from the frame's null-terminated UTF-8 field.</summary>
        public string TrackName { get; set; } = string.Empty;

        /// <summary>Layout name, decoded from the frame's null-terminated UTF-8 field.</summary>
        public string LayoutName { get; set; } = string.Empty;

        /// <summary>Player name.</summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>Player car model identifier.</summary>
        public int PlayerCarModelId { get; set; }

        /// <summary>Player car name.</summary>
        public string PlayerCarName { get; set; } = string.Empty;

        /// <summary>Player class identifier.</summary>
        public int PlayerClassId { get; set; }

        /// <summary>Player class performance index.</summary>
        public int PlayerClassPerfIndex { get; set; }

        /// <summary>Driver count at the first frame. May be exceeded later; see the footer summary.</summary>
        public int NumCarsAtStart { get; set; }

        /// <summary>UTC timestamp at which the first frame was captured.</summary>
        public DateTime StartUtc => new(StartUtcTicks, DateTimeKind.Utc);

        /// <summary>
        /// Absolute byte offset at which <see cref="IndexOffset"/> was last written by
        /// <see cref="Write"/>. The writer patches that eight-byte slot on a clean close, and the
        /// slot cannot be at a constant offset because the version string in front of it is
        /// length-prefixed.
        /// </summary>
        internal long IndexOffsetPosition { get; private set; }

        // Offsets into the raw frame, resolved once. Mirrors the pattern in SharedMemoryService:
        // the layout is owned by Sector3, so it is read from the compiled struct rather than
        // hardcoded.
        private static readonly int versionMajorOffset = OffsetOf(nameof(Shared.VersionMajor));
        private static readonly int versionMinorOffset = OffsetOf(nameof(Shared.VersionMinor));
        private static readonly int trackNameOffset = OffsetOf(nameof(Shared.TrackName));
        private static readonly int layoutNameOffset = OffsetOf(nameof(Shared.LayoutName));
        private static readonly int trackIdOffset = OffsetOf(nameof(Shared.TrackId));
        private static readonly int layoutIdOffset = OffsetOf(nameof(Shared.LayoutId));
        private static readonly int sessionTypeOffset = OffsetOf(nameof(Shared.SessionType));
        private static readonly int sessionPhaseOffset = OffsetOf(nameof(Shared.SessionPhase));
        private static readonly int playerNameOffset = OffsetOf(nameof(Shared.PlayerName));
        private static readonly int vehicleInfoOffset = OffsetOf(nameof(Shared.VehicleInfo));

        private static readonly int carNameOffset =
            vehicleInfoOffset + (int)Marshal.OffsetOf<DriverInfo>(nameof(DriverInfo.Name));
        private static readonly int carModelIdOffset =
            vehicleInfoOffset + (int)Marshal.OffsetOf<DriverInfo>(nameof(DriverInfo.ModelId));
        private static readonly int carClassIdOffset =
            vehicleInfoOffset + (int)Marshal.OffsetOf<DriverInfo>(nameof(DriverInfo.ClassId));
        private static readonly int carClassPerfIndexOffset =
            vehicleInfoOffset + (int)Marshal.OffsetOf<DriverInfo>(nameof(DriverInfo.ClassPerformanceIndex));

        /// <summary>Length of the fixed <c>byte[]</c> string fields in <see cref="Shared"/>.</summary>
        private const int SharedStringLength = 64;

        private static int OffsetOf(string field) => (int)Marshal.OffsetOf<Shared>(field);

        /// <summary>
        /// Populates the layout and metadata fields from a full-size raw frame.
        /// </summary>
        /// <param name="fullFrame">A full-size raw <see cref="Shared"/> frame.</param>
        /// <param name="yaHudVersion">Version string of the running build.</param>
        /// <returns>A header ready to be written.</returns>
        /// <exception cref="ArgumentException"><paramref name="fullFrame"/> is not a full-size frame.</exception>
        public static TelemetryRecordingHeader FromFirstFrame(ReadOnlySpan<byte> fullFrame, string yaHudVersion)
        {
            if (fullFrame.Length < FrameTruncation.SizeOfShared)
            {
                throw new ArgumentException(
                    $"Expected a full frame of {FrameTruncation.SizeOfShared} bytes but got {fullFrame.Length}.",
                    nameof(fullFrame));
            }

            return new TelemetryRecordingHeader
            {
                FormatVersion = CurrentFormatVersion,
                Flags = 0,
                SizeOfShared = FrameTruncation.SizeOfShared,
                AllDriversOffset = FrameTruncation.NumCarsOffset,
                DriverDataSize = FrameTruncation.DriverDataSize,
                R3EVersionMajor = ReadInt(fullFrame, versionMajorOffset),
                R3EVersionMinor = ReadInt(fullFrame, versionMinorOffset),
                YaHudVersion = yaHudVersion ?? string.Empty,
                StartUtcTicks = DateTime.UtcNow.Ticks,
                IndexOffset = 0,
                SessionType = ReadInt(fullFrame, sessionTypeOffset),
                SessionPhaseAtStart = ReadInt(fullFrame, sessionPhaseOffset),
                TrackId = ReadInt(fullFrame, trackIdOffset),
                LayoutId = ReadInt(fullFrame, layoutIdOffset),
                TrackName = ReadString(fullFrame, trackNameOffset),
                LayoutName = ReadString(fullFrame, layoutNameOffset),
                PlayerName = ReadString(fullFrame, playerNameOffset),
                PlayerCarModelId = ReadInt(fullFrame, carModelIdOffset),
                PlayerCarName = ReadString(fullFrame, carNameOffset),
                PlayerClassId = ReadInt(fullFrame, carClassIdOffset),
                PlayerClassPerfIndex = ReadInt(fullFrame, carClassPerfIndexOffset),
                NumCarsAtStart = FrameTruncation.ReadNumCars(fullFrame),
            };
        }

        /// <summary>Writes the header at the current position of <paramref name="writer"/>.</summary>
        /// <param name="writer">A little-endian binary writer positioned at the start of the file.</param>
        public void Write(BinaryWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(Flags);
            writer.Write(SizeOfShared);
            writer.Write(AllDriversOffset);
            writer.Write(DriverDataSize);
            writer.Write(R3EVersionMajor);
            writer.Write(R3EVersionMinor);
            writer.Write(YaHudVersion);
            writer.Write(StartUtcTicks);

            writer.Flush();
            IndexOffsetPosition = writer.BaseStream.Position;
            writer.Write(IndexOffset);

            writer.Write(SessionType);
            writer.Write(SessionPhaseAtStart);
            writer.Write(TrackId);
            writer.Write(LayoutId);
            writer.Write(TrackName);
            writer.Write(LayoutName);
            writer.Write(PlayerName);
            writer.Write(PlayerCarModelId);
            writer.Write(PlayerCarName);
            writer.Write(PlayerClassId);
            writer.Write(PlayerClassPerfIndex);
            writer.Write(NumCarsAtStart);
        }

        /// <summary>Reads and validates a header.</summary>
        /// <param name="reader">A little-endian binary reader positioned at the start of the file.</param>
        /// <returns>The parsed header.</returns>
        /// <exception cref="InvalidDataException">
        /// The magic, format version or <see cref="SizeOfShared"/> does not match this build.
        /// </exception>
        public static TelemetryRecordingHeader Read(BinaryReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            {
                throw new InvalidDataException(
                    "Not a YaHud telemetry recording: the file does not start with the \"YHTL\" magic.");
            }

            var header = new TelemetryRecordingHeader
            {
                FormatVersion = reader.ReadUInt16(),
                Flags = reader.ReadUInt16(),
            };

            if (header.FormatVersion == 0 || header.FormatVersion > CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    $"Recording format version {header.FormatVersion} is not supported by this build " +
                    $"(it understands up to version {CurrentFormatVersion}).");
            }

            header.SizeOfShared = reader.ReadInt32();
            header.AllDriversOffset = reader.ReadInt32();
            header.DriverDataSize = reader.ReadInt32();

            // The version guard. Shared mirrors a layout owned by Sector3; if it has changed since the
            // recording was made, every frame in the file would misparse into plausible-looking
            // garbage. Refuse loudly rather than render nonsense.
            if (header.SizeOfShared != FrameTruncation.SizeOfShared
                || header.AllDriversOffset != FrameTruncation.NumCarsOffset
                || header.DriverDataSize != FrameTruncation.DriverDataSize)
            {
                throw new InvalidDataException(
                    "This recording was captured against a different RaceRoom shared-memory layout and " +
                    "cannot be replayed by this build of YaHud. " +
                    $"Recording: sizeofShared={header.SizeOfShared}, allDriversOffset={header.AllDriversOffset}, " +
                    $"driverDataSize={header.DriverDataSize}. " +
                    $"This build: sizeofShared={FrameTruncation.SizeOfShared}, " +
                    $"allDriversOffset={FrameTruncation.NumCarsOffset}, " +
                    $"driverDataSize={FrameTruncation.DriverDataSize}.");
            }

            header.R3EVersionMajor = reader.ReadInt32();
            header.R3EVersionMinor = reader.ReadInt32();
            header.YaHudVersion = reader.ReadString();
            header.StartUtcTicks = reader.ReadInt64();

            reader.BaseStream.Flush();
            header.IndexOffsetPosition = reader.BaseStream.Position;
            header.IndexOffset = reader.ReadInt64();

            header.SessionType = reader.ReadInt32();
            header.SessionPhaseAtStart = reader.ReadInt32();
            header.TrackId = reader.ReadInt32();
            header.LayoutId = reader.ReadInt32();
            header.TrackName = reader.ReadString();
            header.LayoutName = reader.ReadString();
            header.PlayerName = reader.ReadString();
            header.PlayerCarModelId = reader.ReadInt32();
            header.PlayerCarName = reader.ReadString();
            header.PlayerClassId = reader.ReadInt32();
            header.PlayerClassPerfIndex = reader.ReadInt32();
            header.NumCarsAtStart = reader.ReadInt32();

            if (header.StartUtcTicks < 0 || header.StartUtcTicks > DateTime.MaxValue.Ticks)
            {
                throw new InvalidDataException(
                    $"Recording header carries an implausible start timestamp ({header.StartUtcTicks} ticks).");
            }

            return header;
        }

        /// <summary>
        /// Reads only the header of a recording, without loading the index or decoding any block.
        /// This is what lets the recordings browser show metadata for a whole folder at a glance.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <returns>The parsed header.</returns>
        public static TelemetryRecordingHeader ReadFrom(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            try
            {
                return Read(reader);
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException(
                    $"\"{path}\" is truncated before the end of the recording header.", ex);
            }
        }

        private static int ReadInt(ReadOnlySpan<byte> frame, int offset)
            => BitConverter.ToInt32(frame[offset..]);

        private static string ReadString(ReadOnlySpan<byte> frame, int offset)
        {
            var field = frame.Slice(offset, SharedStringLength);
            var end = field.IndexOf((byte)0);
            if (end < 0)
            {
                end = field.Length;
            }

            return Encoding.UTF8.GetString(field[..end]);
        }
    }
}
