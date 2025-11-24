using R3E.Data;

namespace R3E.API
{
    public class TelemetryData
    {
        /// <summary>
        /// Gets the raw telemetry data as a shared structure.
        /// </summary>
        public Shared Raw { get; internal set; }

        public TelemetryData()
        {
            Raw = new();
        }

        public TelemetryData(Shared raw)
        {
            Raw = raw;
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
        /// Gets the driver directly ahead of the player, if available.
        /// <remarks>This property retrieves the driver in the position immediately ahead of the player's
        /// current position. If there is no driver ahead of the player, the property returns <see
        /// langword="null"/>.</remarks>
        /// </summary>
        public DriverData? CarAhead => GetCarRelative(-1);

        /// <summary>
        /// Gets the driver directly behind the player, if available.
        /// </summary>
        /// <remarks>This property retrieves the driver in the position immediately following the player's
        /// current position. If there is no driver behind the player, the property returns <see
        /// langword="null"/>.</remarks>
        public DriverData? CarBehind => GetCarRelative(1);

        /// <summary>
        /// Retrieves the driver data relative to the player's position in the race order.
        /// </summary>
        /// <remarks>This method assumes that the player's position is valid and within the bounds of the
        /// driver data array.  If the player's position is invalid or the driver data array is unavailable, the method
        /// returns <see langword="null"/>.</remarks>
        /// <param name="offset">The relative position offset from the player's current position.  A negative value retrieves a driver ahead
        /// of the player, while a positive value retrieves a driver behind.</param>
        /// <returns>The <see cref="DriverData"/> of the driver at the specified relative position, or <see langword="null"/>  if
        /// the position is out of bounds or no driver data is available.</returns>
        public DriverData? GetCarRelative(int offset)
        {
            var idx = PlayerDriverIndex;
            if (idx < 0) return null;

            if (Raw.DriverData is null) return null;

            var target = idx + offset;
            if (target < 0 || target >= Raw.DriverData.Length) return null;

            // DriverData is a struct; wrap as nullable to represent "not present"
            return Raw.DriverData[target];
        }
    }
}
