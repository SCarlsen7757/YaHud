using R3E.Data;

namespace R3E.API
{
    public class TelemetryData
    {
        // Raw telemetry struct
        public Shared Raw { get; internal set; }

        public TelemetryData()
        {
            Raw = new();
        }

        public TelemetryData(Shared raw)
        {
            Raw = raw;
        }

        // Player position (1-based as provided by the shared memory; -1 or 0 indicates unknown)
        public int PlayerPosition => Raw.Position;

        // Helper: get the zero-based index into DriverData for the player, or -1 if not available
        public int PlayerDriverIndex
        {
            get
            {
                if (PlayerPosition <= 0) return -1;
                return PlayerPosition - 1;
            }
        }

        // Returns the driver directly ahead of the player if available
        public DriverData? CarAhead => GetCarRelative(-1);

        // Returns the driver directly behind the player if available
        public DriverData? CarBehind => GetCarRelative(1);

        // Generic accessor to get driver relative to the player's position (offset -1 => ahead, +1 => behind)
        private DriverData? GetCarRelative(int offset)
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
