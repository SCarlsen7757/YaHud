using R3E.API.TimeGap;
using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class TelemetryData
    {
        /// <summary>
        /// Gets the raw telemetry data as a shared structure.
        /// </summary>
        public Shared Raw { get; internal set; }

        private readonly TimeGapService? timeGapService;

        public TelemetryData(TimeGapService timeGapService)
        {
            this.timeGapService = timeGapService;
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
        /// driver data array. If the player's position is invalid or the driver data array is unavailable, the method
        /// returns <see langword="null"/>.</remarks>
        /// <param name="offset">The relative position offset from the player's current position. A negative value retrieves a driver ahead
        /// of the player, while a positive value retrieves a driver behind.</param>
        /// <returns>The <see cref="DriverData"/> of the driver at the specified relative position, or <see langword="null"/> if
        /// the position is out of bounds or no driver data is available.</returns>
        public DriverData? GetCarRelative(int offset)
        {
            var idx = PlayerDriverIndex;
            if (idx < 0) return null;

            if (Raw.DriverData is null) return null;

            var target = idx + offset;
            if (target < 0 || target >= Raw.DriverData.Length) return null;

            return Raw.DriverData[target];
        }

        /// <summary>
        /// Gets drivers relative to the player based on physical track position (circular logic).
        /// Dynamically allocates display slots based on time gaps.
        /// </summary>
        /// <param name="maxDrivers">Maximum total number of drivers to display</param>
        /// <returns>List of drivers sorted by track position (leader first)</returns>
        public IList<RelativeDriverInfo> GetRelativeDrivers(int maxDrivers = 7)
        {
            List<RelativeDriverInfo> result = [];

            if (timeGapService == null)
                throw new InvalidOperationException($"No instance of {nameof(TimeGapService)} found.");
            if (Raw.NumCars <= 0 || Raw.DriverData == null || maxDrivers <= 0) return result;

            var playerSlotId = Raw.VehicleInfo.SlotId;
            var playerLapFraction = Raw.LapDistanceFraction;
            var trackLength = Raw.LayoutLength;

            // 1. Calculate standardized circular relative difference for ALL drivers
            // This handles the start/finish line wrap-around correctly.
            var relativeDrivers = Raw.DriverData
                .Take(Raw.NumCars)
                .Where(d => d.DriverInfo.SlotId >= 0)
                .Select(d =>
                {
                    double diff = d.LapDistanceFraction - playerLapFraction;

                    // Normalize diff to range [-0.5, 0.5] to find shortest path on circle
                    if (diff > 0.5) diff -= 1.0;
                    else if (diff < -0.5) diff += 1.0;

                    return new { Driver = d, RelativeDiff = diff };
                })
                .OrderByDescending(x => x.RelativeDiff) // Sort Descending: Ahead (+ve) -> Player (0) -> Behind (-ve)
                .ToList();

            // Find Player in the sorted list
            var playerIndex = relativeDrivers.FindIndex(x => x.Driver.DriverInfo.SlotId == playerSlotId);
            if (playerIndex < 0) return result; // Should not happen if player is in the list

            // 2. Determine how many slots to allocate for "Ahead" vs "Behind"
            int slotsForOthers = maxDrivers - 1;
            int maxAheadSlots = slotsForOthers / 2;
            const double timeGapThresholdSeconds = 10.0d;

            // Count how many cars immediately ahead are within the time threshold
            int foundAheadCount = 0;

            // Scan upwards from player (Index - 1 is closest ahead)
            for (int i = 1; i <= maxAheadSlots; i++)
            {
                int idx = playerIndex - i;
                if (idx < 0) break; // End of list

                var item = relativeDrivers[idx];

                // Calculate relative time gap
                float gap = timeGapService.GetTimeGapRelative(playerSlotId, item.Driver.DriverInfo.SlotId);

                // Gap > 0 means subject (Player) is behind target (Item). This confirms valid "Ahead" car.
                if (gap > 0 && gap <= timeGapThresholdSeconds)
                {
                    foundAheadCount++;
                }
                else
                {
                    // Stop scanning if we hit a large gap (>10s) so we can allocate more slots to cars behind
                    break;
                }
            }

            int countAhead = foundAheadCount;
            int countBehind = slotsForOthers - countAhead;

            // 3. Determine index range in the sorted list
            // We want [Player - countAhead] to [Player + countBehind]
            int startIndex = playerIndex - countAhead;
            int endIndex = playerIndex + countBehind;

            // Adjust window if we hit bounds (e.g. not enough cars ahead/behind in the session)
            // Shift window down if start is negative
            if (startIndex < 0)
            {
                endIndex += (0 - startIndex);
                startIndex = 0;
            }
            // Shift window up if end is out of bounds
            if (endIndex >= relativeDrivers.Count)
            {
                startIndex -= (endIndex - (relativeDrivers.Count - 1));
                endIndex = relativeDrivers.Count - 1;
            }

            // Clamp finally (in case total cars < maxDrivers)
            startIndex = Math.Max(0, startIndex);
            endIndex = Math.Min(relativeDrivers.Count - 1, endIndex);

            // 4. Build Result List
            for (int i = startIndex; i <= endIndex; i++)
            {
                var item = relativeDrivers[i];
                bool isPlayer = (i == playerIndex);
                TimeSpan timeGap = TimeSpan.Zero;

                if (!isPlayer)
                {
                    float relGap = timeGapService.GetTimeGapRelative(playerSlotId, item.Driver.DriverInfo.SlotId);

                    // Convert to TimeSpan.
                    // If relGap > 0 (Player is Behind, Car is Ahead), we typically show negative time (e.g. -1.2s)
                    // If relGap < 0 (Player is Ahead, Car is Behind), we typically show positive time (e.g. +1.2s)
                    timeGap = TimeSpan.FromSeconds(-relGap);
                }

                float distanceGap = (float)item.RelativeDiff * trackLength;

                result.Add(new RelativeDriverInfo
                {
                    DriverData = item.Driver,
                    IsPlayer = isPlayer,
                    LapDifference = item.Driver.CompletedLaps - Raw.CompletedLaps,
                    DistanceGap = distanceGap,
                    TimeGap = timeGap
                });
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