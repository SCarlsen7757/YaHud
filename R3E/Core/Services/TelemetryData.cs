using R3E.Data;
using R3E.Extensions;

namespace R3E.Core.Services
{
    public class TelemetryData
    {
        /// <summary>
        /// Gets the raw telemetry data as a shared structure.
        /// </summary>
        public Shared Raw { get; internal set; } = new Shared();

        public TelemetryData()
        {
        }

        /// <summary>
        /// Gets the player's starting position in the race session, as a 1-based index.
        /// </summary>
        public int PlayerStartPosition { get; internal set; }

        /// <summary>
        /// Gets the current position of the player in the race, using a 1-based index.
        /// </summary>
        public int PlayerPosition => Raw.Position;

        /// <summary>
        /// Gets the zero-based index of the player in the driver data, or -1 if the player's position is not available.
        /// </summary>
        public int PlayerDriverIndex
        {
            get
            {
                if (PlayerPosition <= 0) return -1;
                return PlayerPosition - 1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the start is a rolling start.
        /// </summary>
        public bool RollingStart { get; internal set; }

        public static string GetDriverName(byte[] nameBytes)
        {
            return nameBytes.ToNullTerminatedString();
        }
    }
}