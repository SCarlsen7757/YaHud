using R3E.Core.Services;
using R3E.Data;

namespace R3E.Features.Driver
{
    /// <summary>
    /// Pure data class for relative driver information.
    /// Contains only simple properties and basic lookups - no complex logic.
    /// </summary>
    public class DriverData
    {
        private readonly TelemetryData telemetryData;
        private Shared Raw => telemetryData.Raw;

        internal DriverData(TelemetryData telemetryData)
        {
            this.telemetryData = telemetryData;
        }

        /// <summary>
        /// Gets the driver directly ahead of the player, if available.
        /// </summary>
        public Data.DriverData? CarAhead => GetCarRelative(-1);

        /// <summary>
        /// Gets the driver directly behind the player, if available.
        /// </summary>
        public Data.DriverData? CarBehind => GetCarRelative(1);

        /// <summary>
        /// Retrieves the driver data relative to the player's position in the race order.
        /// </summary>
        /// <param name="offset">The relative position offset from the player's current position. 
        /// A negative value retrieves a driver ahead of the player, while a positive value retrieves a driver behind.</param>
        /// <returns>The driver at the specified relative position, or null if out of bounds.</returns>
        public Data.DriverData? GetCarRelative(int offset)
        {
            var idx = telemetryData.PlayerDriverIndex;
            if (idx < 0) return null;

            if (Raw.DriverData is null) return null;

            var target = idx + offset;
            if (target < 0 || target >= Raw.DriverData.Length) return null;

            return Raw.DriverData[target];
        }
    }
}
