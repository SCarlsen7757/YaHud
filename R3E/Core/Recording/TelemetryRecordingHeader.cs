using R3E.Data;

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
        /// Populates the layout and metadata fields from a full-size raw frame.
        /// </summary>
        /// <param name="fullFrame">A full-size raw <see cref="Shared"/> frame.</param>
        /// <param name="yaHudVersion">Version string of the running build.</param>
        /// <returns>A header ready to be written.</returns>
        /// <remarks>Implemented by agent B1.</remarks>
        public static TelemetryRecordingHeader FromFirstFrame(ReadOnlySpan<byte> fullFrame, string yaHudVersion)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>Writes the header at the current position of <paramref name="writer"/>.</summary>
        /// <param name="writer">A little-endian binary writer positioned at the start of the file.</param>
        /// <remarks>Implemented by agent B1.</remarks>
        public void Write(BinaryWriter writer)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>Reads and validates a header.</summary>
        /// <param name="reader">A little-endian binary reader positioned at the start of the file.</param>
        /// <returns>The parsed header.</returns>
        /// <exception cref="InvalidDataException">
        /// The magic, format version or <see cref="SizeOfShared"/> does not match this build.
        /// </exception>
        /// <remarks>Implemented by agent B1.</remarks>
        public static TelemetryRecordingHeader Read(BinaryReader reader)
            => throw new NotImplementedException("Implemented by agent B1");

        /// <summary>
        /// Reads only the header of a recording, without loading the index or decoding any block.
        /// This is what lets the recordings browser show metadata for a whole folder at a glance.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <returns>The parsed header.</returns>
        /// <remarks>Implemented by agent B1.</remarks>
        public static TelemetryRecordingHeader ReadFrom(string path)
            => throw new NotImplementedException("Implemented by agent B1");
    }
}
