using R3E.Data;
using System.Runtime.InteropServices;

namespace R3E.Core.Recording
{
    /// <summary>
    /// Truncates and restores raw <see cref="Shared"/> frames around the variable-size driver tail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>NumCars</c> followed by <c>DriverData[128]</c> are the last fields of <see cref="Shared"/>,
    /// so "store only the drivers that exist" is a pure byte-range truncation: keep everything up to
    /// and including <c>NumCars</c>, then keep exactly <c>NumCars</c> driver slots. Restoring is a
    /// zero-filled buffer plus a copy — absent driver slots are zeros, which is exactly what the game
    /// would have had for them.
    /// </para>
    /// <para>
    /// Offsets are resolved once via <see cref="Marshal.OffsetOf{T}(string)"/>, mirroring the pattern
    /// already used by <c>SharedMemoryService</c>, so the code stays correct if the layout moves.
    /// </para>
    /// </remarks>
    public static class FrameTruncation
    {
        /// <summary>Maximum number of driver slots in <see cref="Shared"/>.</summary>
        public const int MaxDrivers = 128;

        /// <summary>Full marshalled size of <see cref="Shared"/>, in bytes.</summary>
        public static int SizeOfShared { get; }

        /// <summary>Byte offset of <c>Shared.NumCars</c>, i.e. the start of the variable-size tail.</summary>
        public static int NumCarsOffset { get; }

        /// <summary>Marshalled size of a single <see cref="DriverData"/> entry, in bytes.</summary>
        public static int DriverDataSize { get; }

        /// <summary>Smallest valid truncated frame: everything up to and including <c>NumCars</c>.</summary>
        public static int MinTruncatedLength => NumCarsOffset + sizeof(int);

        static FrameTruncation()
        {
            SizeOfShared = Marshal.SizeOf<Shared>();
            NumCarsOffset = (int)Marshal.OffsetOf<Shared>(nameof(Shared.NumCars));
            DriverDataSize = Marshal.SizeOf<DriverData>();
        }

        /// <summary>
        /// Reads <c>NumCars</c> out of a raw frame without marshalling, clamped to
        /// <c>[0, <see cref="MaxDrivers"/>]</c> so a garbage frame cannot produce a wild length.
        /// </summary>
        /// <param name="frame">A full or truncated raw frame.</param>
        /// <returns>The clamped driver count, or 0 if the frame is too short to contain the field.</returns>
        public static int ReadNumCars(ReadOnlySpan<byte> frame)
        {
            if (frame.Length < MinTruncatedLength)
            {
                return 0;
            }

            var numCars = BitConverter.ToInt32(frame[NumCarsOffset..]);
            return Math.Clamp(numCars, 0, MaxDrivers);
        }

        /// <summary>
        /// Slices the populated prefix out of a full-size raw frame.
        /// </summary>
        /// <param name="full">A full-size raw frame, <see cref="SizeOfShared"/> bytes.</param>
        /// <param name="length">Receives the truncated length in bytes.</param>
        /// <returns>The truncated slice of <paramref name="full"/>. No copy is made.</returns>
        /// <exception cref="ArgumentException"><paramref name="full"/> is not a full-size frame.</exception>
        public static ReadOnlySpan<byte> Truncate(ReadOnlySpan<byte> full, out int length)
        {
            if (full.Length != SizeOfShared)
            {
                throw new ArgumentException(
                    $"Expected a full frame of {SizeOfShared} bytes but got {full.Length}.", nameof(full));
            }

            var numCars = ReadNumCars(full);
            length = MinTruncatedLength + (numCars * DriverDataSize);

            // Cannot exceed the source; belt and braces against a layout mismatch.
            if (length > full.Length)
            {
                length = full.Length;
            }

            return full[..length];
        }

        /// <summary>
        /// Rebuilds a full-size raw frame from a truncated one. Driver slots beyond <c>NumCars</c>
        /// are zeroed.
        /// </summary>
        /// <param name="truncated">A frame previously produced by <see cref="Truncate"/>.</param>
        /// <param name="full">Destination buffer of exactly <see cref="SizeOfShared"/> bytes.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="full"/> is not <see cref="SizeOfShared"/> bytes, or
        /// <paramref name="truncated"/> is too short or longer than a full frame.
        /// </exception>
        public static void Restore(ReadOnlySpan<byte> truncated, Span<byte> full)
        {
            if (full.Length != SizeOfShared)
            {
                throw new ArgumentException(
                    $"Destination must be {SizeOfShared} bytes but was {full.Length}.", nameof(full));
            }

            if (truncated.Length < MinTruncatedLength || truncated.Length > SizeOfShared)
            {
                throw new ArgumentException(
                    $"Truncated frame length {truncated.Length} is outside [{MinTruncatedLength}, {SizeOfShared}].",
                    nameof(truncated));
            }

            full.Clear();
            truncated.CopyTo(full);
        }

        /// <summary>
        /// Allocates and rebuilds a full-size raw frame. Convenience wrapper over
        /// <see cref="Restore(ReadOnlySpan{byte}, Span{byte})"/> for callers without a pooled buffer.
        /// </summary>
        /// <param name="truncated">A frame previously produced by <see cref="Truncate"/>.</param>
        /// <returns>A newly allocated full-size frame.</returns>
        public static byte[] Restore(ReadOnlySpan<byte> truncated)
        {
            var full = new byte[SizeOfShared];
            Restore(truncated, full);
            return full;
        }

        /// <summary>
        /// Cross-checks the frame's own self-describing layout fields (<c>AllDriversOffset</c> and
        /// <c>DriverDataSize</c>) against the offsets resolved from the compiled struct.
        /// </summary>
        /// <param name="frame">A full or truncated raw frame.</param>
        /// <returns><see langword="true"/> when the frame's layout matches this build.</returns>
        public static bool MatchesLayout(ReadOnlySpan<byte> frame)
        {
            var allDriversOffset = (int)Marshal.OffsetOf<Shared>(nameof(Shared.AllDriversOffset));
            var driverDataSizeOffset = (int)Marshal.OffsetOf<Shared>(nameof(Shared.DriverDataSize));

            if (frame.Length < driverDataSizeOffset + sizeof(int))
            {
                return false;
            }

            var reportedOffset = BitConverter.ToInt32(frame[allDriversOffset..]);
            var reportedSize = BitConverter.ToInt32(frame[driverDataSizeOffset..]);

            // A frame that has never been written by the game reports zeros; treat that as unknown
            // rather than as a mismatch.
            if (reportedOffset == 0 && reportedSize == 0)
            {
                return true;
            }

            return reportedOffset == NumCarsOffset && reportedSize == DriverDataSize;
        }
    }
}
