using R3E.Data;
using System.Text;

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

        /// <summary>
        /// Gets drivers relative to the player based on physical track position.
        /// Returns up to 'ahead' cars in front and 'behind' cars behind the player on track.
        /// </summary>
        /// <param name="ahead">Number of cars ahead to retrieve (default 3)</param>
        /// <param name="behind">Number of cars behind to retrieve (default 3)</param>
        /// <returns>List of drivers sorted by track position (leader first)</returns>
        public List<RelativeDriverInfo> GetRelativeDrivers(int ahead = 3, int behind = 3)
        {
            var result = new List<RelativeDriverInfo>();

            if (Raw.NumCars <= 0 || Raw.DriverData == null)
            {
                return result;
            }

            var playerSlotId = Raw.VehicleInfo.SlotId;
            var trackLength = Raw.LayoutLength;
            var playerTotalDistance = (Raw.CompletedLaps * trackLength) + Raw.LapDistance;

            // Sort by physical track position (not race position)
            var sortedDrivers = Raw.DriverData
                .Take(Raw.NumCars)
                .Where(d => d.DriverInfo.SlotId >= 0)
                .Select(d => new
                {
                    Driver = d,
                    TotalDistance = (d.CompletedLaps * trackLength) + d.LapDistance
                })
                .OrderByDescending(x => x.TotalDistance)
                .ToList();

            var playerIndex = sortedDrivers.FindIndex(d => d.Driver.DriverInfo.SlotId == playerSlotId);
            if (playerIndex < 0)
            {
                return result;
            }

            // Get drivers in range
            var startIndex = Math.Max(0, playerIndex - ahead);
            var endIndex = Math.Min(sortedDrivers.Count - 1, playerIndex + behind);

            for (int i = startIndex; i <= endIndex; i++)
            {
                var driver = sortedDrivers[i].Driver;
                var driverTotalDistance = sortedDrivers[i].TotalDistance;
                var isPlayer = driver.DriverInfo.SlotId == playerSlotId;

                var relativeInfo = new RelativeDriverInfo
                {
                    DriverData = driver,
                    IsPlayer = isPlayer,
                    LapDifference = driver.CompletedLaps - Raw.CompletedLaps,
                    DistanceGap = isPlayer ? 0f : (float)(driverTotalDistance - playerTotalDistance),
                    TimeGap = CalculateTimeGap(driver, driverTotalDistance, playerTotalDistance, isPlayer)
                };

                result.Add(relativeInfo);
            }

            return result;
        }

        private TimeSpan CalculateTimeGap(DriverData driver, double driverTotalDistance, double playerTotalDistance, bool isPlayer)
        {
            if (isPlayer) return TimeSpan.Zero;

            var distanceDiff = driverTotalDistance - playerTotalDistance;

            if (distanceDiff > 0)
            {
                // Driver is ahead - use negative time
                return TimeSpan.FromSeconds(-Math.Abs(driver.TimeDeltaBehind));
            }
            else
            {
                // Driver is behind - use positive time
                return TimeSpan.FromSeconds(Math.Abs(driver.TimeDeltaBehind));
            }
        }

        /// <summary>
        /// Gets the driver name from a DriverInfo name byte array.
        /// </summary>
        /// <param name="nameBytes">UTF-8 encoded byte array containing the driver name</param>
        /// <returns>Driver name as string, or "" if invalid</returns>
        public static string GetDriverName(byte[] nameBytes)
        {
            return ByteArrayToString(nameBytes);
        }

        /// <summary>
        /// Converts a null-terminated UTF-8 byte array to a string.
        /// </summary>
        /// <param name="bytes">UTF-8 encoded byte array</param>
        /// <returns>Decoded string</returns>
        private static string ByteArrayToString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            // Find the null terminator
            var endIndex = Array.IndexOf(bytes, (byte)0);
            if (endIndex < 0)
            {
                endIndex = bytes.Length;
            }

            return Encoding.UTF8.GetString(bytes, 0, endIndex);
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
