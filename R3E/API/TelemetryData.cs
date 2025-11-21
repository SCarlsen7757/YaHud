using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class TelemetryData
    {
        // Raw telemetry struct
        public Shared Raw { get; internal set; }

        private DataPointService? dataPointService;

        public void SetDataPointService(DataPointService dataPointService)
        {
            this.dataPointService = dataPointService;
        }

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

        /// <summary>
        /// Gets drivers relative to the player based on physical track position.
        /// Dynamically allocates display slots based on time gaps - if cars ahead are >10s away,
        /// their slots are reallocated to show more cars behind.
        /// </summary>
        /// <param name="maxDrivers">Maximum total number of drivers to display (default 7, includes player)</param>
        /// <returns>List of drivers sorted by track position (leader first)</returns>
        public IList<RelativeDriverInfo> GetRelativeDrivers(int maxDrivers = 7)
        {
            List<RelativeDriverInfo> result = [];

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

            var test = sortedDrivers.Select(x => x.DriverInfo.Name.ToNullTerminatedString());



            var playerIndex = sortedDrivers.FindIndex(d => d.DriverInfo.SlotId == playerSlotId);
            if (playerIndex < 0)
                throw new InvalidDataException("Player driver not found in DriverData");

            var playerDriver = sortedDrivers[playerIndex];

            int slotsForOthers = maxDrivers - 1; // Reserve 1 slot for player
            int maxAheadSlots = slotsForOthers / 2;
            int defaultBehind = slotsForOthers - maxAheadSlots;

            // Count how many cars ahead are within 10 seconds (up to maxAheadSlots)
            int carsAheadWithin10s = 0;
            for (int i = playerIndex - 1; i >= Math.Max(0, playerIndex - maxAheadSlots); i--)
            {
                var driver = sortedDrivers[i];
                var timeGap = Math.Abs(driver.TimeDeltaBehind);

                if (timeGap <= 10.0)
                {
                    carsAheadWithin10s++;
                }
            }

            int actualAhead = Math.Min(carsAheadWithin10s, maxAheadSlots);
            int actualBehind = slotsForOthers - actualAhead;

            // Get drivers in range
            var startIndex = Math.Max(0, playerIndex - actualAhead);
            var endIndex = Math.Min(sortedDrivers.Count - 1, playerIndex + actualBehind);

            var ahead = true;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var driver = sortedDrivers[i];
                var isPlayer = driver.DriverInfo.SlotId == playerSlotId;

                if (isPlayer) ahead = false;

                // Calculate time gap using DataPointService if available, otherwise fall back to raw data
                TimeSpan timeGap;
                if (dataPointService != null && !isPlayer)
                {
                    // Use DataPointService to get more accurate time gap based on position history
                    var playerSlotId_local = playerSlotId;
                    var driverSlotId = driver.DriverInfo.SlotId;

                    if (ahead)
                    {
                        // Driver is ahead, get time gap from DataPointService
                        float gap = dataPointService.GetTimeGap(playerSlotId_local, driverSlotId);
                        timeGap = TimeSpan.FromSeconds(-Math.Abs(gap)); // Negative because ahead
                    }
                    else
                    {
                        // Driver is behind, get time gap from DataPointService
                        float gap = dataPointService.GetTimeGap(driverSlotId, playerSlotId_local);
                        timeGap = TimeSpan.FromSeconds(Math.Abs(gap)); // Positive because behind
                    }
                }
                else
                {
                    // Fall back to raw TimeDelta values from shared memory
                    var rawTimeGap = ahead ? driver.TimeDeltaBehind : driver.TimeDeltaFront;
                    timeGap = TimeSpan.FromSeconds(rawTimeGap);
                }

                var relativeInfo = new RelativeDriverInfo
                {
                    DriverData = driver,
                    IsPlayer = isPlayer,
                    LapDifference = driver.CompletedLaps - Raw.CompletedLaps,
                    DistanceGap = isPlayer ? 0f : 0f,
                    TimeGap = isPlayer ? TimeSpan.Zero : timeGap
                };

                result.Add(relativeInfo);
            }

            return result;
        }

        //private TimeSpan CalculateTimeGap(DriverData driver, double driverTotalDistance, double playerTotalDistance, bool isPlayer)
        //{
        //    if (isPlayer) return TimeSpan.Zero;

        //    var distanceDiff = driverTotalDistance - playerTotalDistance;

        //    if (distanceDiff > 0)
        //    {
        //        // Driver is ahead - use negative time
        //        return TimeSpan.FromSeconds(-Math.Abs(driver.TimeDeltaBehind));
        //    }
        //    else
        //    {
        //        // Driver is behind - use positive time
        //        return TimeSpan.FromSeconds(Math.Abs(driver.TimeDeltaBehind));
        //    }
        //}

        /// <summary>
        /// Gets the driver name from a DriverInfo name byte array.
        /// </summary>
        /// <param name="nameBytes">UTF-8 encoded byte array containing the driver name</param>
        /// <returns>Driver name as string, or "" if invalid</returns>
        public static string GetDriverName(byte[] nameBytes)
        {
            return nameBytes.ToNullTerminatedString();
        }
    }

    /// <summary>
    /// Represents a driver's position relative to the player
    /// </summary>
    public class RelativeDriverInfo
    {
        public DriverData DriverData { get; set; }
        public bool IsPlayer { get; set; }
        public int LapDifference { get; set; }
        public float DistanceGap { get; set; } // In meters
        public TimeSpan TimeGap { get; set; }
    }
}
