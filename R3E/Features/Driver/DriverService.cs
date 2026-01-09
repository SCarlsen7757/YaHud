using R3E.Core.Interfaces;
using R3E.Core.Services;
using R3E.Features.TimeGap;

namespace R3E.Features.Driver
{
    /// <summary>
    /// Service for calculating relative driver positions and time gaps.
    /// Handles complex logic for determining which drivers to display based on track position and time gaps.
    /// </summary>
    public class DriverService
    {
        private readonly ITelemetryService telemetry;
        private readonly ITimeGapService timeGapService;
        private readonly ILogger<DriverService> logger;
        private readonly TelemetryData telemetryData;

        /// <summary>
        /// Simple relative driver data (car ahead/behind lookups).
        /// </summary>
        public DriverData Data { get; }

        public DriverService(
            ITelemetryService telemetry,
            ITimeGapService timeGapService,
            ILogger<DriverService> logger)
        {
            this.telemetry = telemetry;
            this.timeGapService = timeGapService;
            this.logger = logger;

            // Store reference to TelemetryData once
            telemetryData = telemetry.Data;
            Data = new DriverData(telemetryData);
        }

        /// <summary>
        /// Gets drivers relative to the player based on physical track position (circular logic).
        /// Dynamically allocates display slots based on time gaps.
        /// </summary>
        /// <param name="maxDrivers">Maximum total number of drivers to display</param>
        /// <returns>List of drivers sorted by track position (leader first)</returns>
        public IList<DriverInfo> GetRelativeDrivers(int maxDrivers = 7)
        {
            List<DriverInfo> result = [];

            var raw = telemetryData.Raw;

            if (raw.NumCars <= 0 || raw.DriverData == null || maxDrivers <= 0)
                return result;

            var playerSlotId = raw.VehicleInfo.SlotId;
            var playerLapFraction = raw.LapDistanceFraction;
            var trackLength = raw.LayoutLength;

            // 1. Calculate standardized circular relative difference for ALL drivers
            // This handles the start/finish line wrap-around correctly.
            var relativeDrivers = raw.DriverData
                .Take(raw.NumCars)
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

                result.Add(new DriverInfo
                {
                    DriverData = item.Driver,
                    IsPlayer = isPlayer,
                    LapDifference = item.Driver.CompletedLaps - raw.CompletedLaps,
                    DistanceGap = distanceGap,
                    TimeGap = timeGap
                });
            }

            return result;
        }
    }
}
