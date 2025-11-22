using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class TelemetryData
    {
        // Raw telemetry struct
        public Shared Raw { get; internal set; } = new();

        private readonly DataPointService? dataPointService;

        public TelemetryData(DataPointService dataPointService)
        {
            this.dataPointService = dataPointService;
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

            return Raw.DriverData[target];
        }

        /// <summary>
        /// Gets drivers relative to the player based on physical track position.
        /// Dynamically allocates display slots based on time gaps.
        /// </summary>
        /// <param name="maxDrivers">Maximum total number of drivers to display</param>
        /// <returns>List of drivers sorted by track position (leader first)</returns>
        public IList<RelativeDriverInfo> GetRelativeDrivers(int maxDrivers = 7)
        {
            List<RelativeDriverInfo> result = [];

            if (dataPointService == null)
                throw new InvalidOperationException("DataPointService not set. Call SetDataPointService() before using GetRelativeDrivers().");
            if (Raw.NumCars <= 0 || Raw.DriverData == null || maxDrivers <= 0) return result;

            var playerSlotId = Raw.VehicleInfo.SlotId;
            var playerLapFraction = Raw.LapDistanceFraction;
            var trackLength = Raw.LayoutLength;

            // Sort by physical track position
            var sortedDrivers = Raw.DriverData
                .Take(Raw.NumCars)
                .Where(d => d.DriverInfo.SlotId >= 0)
                .OrderByDescending(x => x.LapDistanceFraction)
                .ToList();

            var playerIndex = sortedDrivers.FindIndex(d => d.DriverInfo.SlotId == playerSlotId);
            if (playerIndex < 0)
                throw new InvalidDataException("Player driver not found in DriverData");

            int slotsForOthers = maxDrivers - 1;
            int maxAheadSlots = slotsForOthers / 2;

            const double timeGapThresholdSeconds = 10.0d;

            // Count how many cars ahead are within 10 seconds (using Relative Time Gap)
            List<DriverData> carsAheadWithin10s = [];

            // Check cars physically ahead in the list
            for (int i = playerIndex - 1; i >= 0; i--)
            {
                var driver = sortedDrivers[i];
                var timeGap = dataPointService.GetTimeGapRelative(playerSlotId, driver.DriverInfo.SlotId);

                // Note: GetTimeGapRelative returns positive if Subject(Player) is BEHIND Target(Driver)
                // Since we are checking cars 'ahead', we expect positive gap from Player -> Driver?
                // No, GetTimeGapRelative(Subject=Player, Target=Driver):
                // If Driver is ahead, Player is behind -> Positive Gap.

                if (timeGap > 0 && timeGap <= timeGapThresholdSeconds)
                {
                    carsAheadWithin10s.Add(driver);
                    if (carsAheadWithin10s.Count >= maxAheadSlots) break;
                }
            }

            // Check wrap-around (cars physically ahead but at end of list)
            if (carsAheadWithin10s.Count < maxAheadSlots)
            {
                for (int i = sortedDrivers.Count - 1; i > playerIndex; i--)
                {
                    var driver = sortedDrivers[i];
                    var timeGap = dataPointService.GetTimeGapRelative(playerSlotId, driver.DriverInfo.SlotId);

                    if (timeGap > 0 && timeGap <= timeGapThresholdSeconds)
                    {
                        carsAheadWithin10s.Add(driver);
                        if (carsAheadWithin10s.Count >= maxAheadSlots) break;
                    }
                }
            }

            int actualAhead = carsAheadWithin10s.Count;
            int actualBehind = slotsForOthers - actualAhead;

            // Get drivers in range
            var startIndex = Math.Max(0, playerIndex - actualAhead);
            var endIndex = Math.Min(sortedDrivers.Count - 1, playerIndex + actualBehind);

            for (int i = startIndex; i <= endIndex; i++)
            {
                var driver = sortedDrivers[i];
                var isPlayer = driver.DriverInfo.SlotId == playerSlotId;

                TimeSpan timeGap = TimeSpan.Zero;
                float distanceGap = 0f;

                if (!isPlayer)
                {
                    var driverSlotId = driver.DriverInfo.SlotId;

                    // Use GetTimeGapRelative which handles lap differences automatically
                    // We ask: What is gap between Player and Driver?
                    float relGap = dataPointService.GetTimeGapRelative(playerSlotId, driverSlotId);

                    // If relGap is positive, Player is BEHIND Driver (Driver is Ahead) -> Gap should be negative for display (standard racing UI)
                    // If relGap is negative, Player is AHEAD OF Driver (Driver is Behind) -> Gap should be positive

                    timeGap = TimeSpan.FromSeconds(-relGap);
                }

                // Calculate distance gap
                float lapFractionDiff = driver.LapDistanceFraction - playerLapFraction;

                // Normalize lap fraction diff to shortest path [-0.5, 0.5]
                if (lapFractionDiff > 0.5f) lapFractionDiff -= 1.0f;
                else if (lapFractionDiff < -0.5f) lapFractionDiff += 1.0f;

                distanceGap = lapFractionDiff * trackLength;

                var relativeInfo = new RelativeDriverInfo
                {
                    DriverData = driver,
                    IsPlayer = isPlayer,
                    LapDifference = driver.CompletedLaps - Raw.CompletedLaps,
                    DistanceGap = distanceGap,
                    TimeGap = timeGap,
                };

                result.Add(relativeInfo);
            }

            return result;
        }

        public static string GetDriverName(byte[] nameBytes)
        {
            return nameBytes.ToNullTerminatedString();
        }
    }

    public class RelativeDriverInfo
    {
        public DriverData DriverData { get; set; }
        public bool IsPlayer { get; set; }
        public int LapDifference { get; set; }
        public float DistanceGap { get; set; }
        public TimeSpan TimeGap { get; set; }
    }
}