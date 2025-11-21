using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class DataPointService : IDisposable
    {
        private readonly ILogger<DataPointService> logger;
        private readonly ISharedSource sharedSource;

        // Dictionary to hold history for every car.
        // Key = SlotId (Unique ID for the driver in the session).
        private readonly Dictionary<int, CarHistory> carHistories = [];
        private readonly Lock @lock = new(); // Lock for thread safety

        private double lastPruneTime = 0;
        private const double PruneIntervalSeconds = 10.0; // Clean up data every 10 seconds
        private float trackLength = 0;

        public DataPointService(ILogger<DataPointService> logger, ISharedSource sharedSource)
        {
            this.logger = logger;
            this.sharedSource = sharedSource;
            this.sharedSource.DataUpdated += OnDataUpdated;

            this.logger.LogInformation("DataPointService initialized");
        }

        /// <summary>
        /// Returns the absolute time gap in seconds between two cars (including lap differences).
        /// Positive value means 'subject' is BEHIND 'target'.
        /// </summary>
        public float GetTimeGap(int subjectSlotId, int targetSlotId)
        {
            lock (@lock)
            {
                // We need the subject's current location
                if (!carHistories.TryGetValue(subjectSlotId, out var subjectHistory) ||
                    !carHistories.TryGetValue(targetSlotId, out var targetHistory))
                {
                    return 0f;
                }

                // Get the exact current total distance of the subject car
                double subjectTotalDistance = subjectHistory.LastKnownDistance;
                double currentSessionTime = subjectHistory.LastKnownTime;

                // Find when the target car was at this specific distance
                double targetTimeAtDist = targetHistory.GetTimeAtDistance(subjectTotalDistance);

                if (targetTimeAtDist <= 0)
                    return 0f; // Data not found (target hasn't reached here yet or history missing)

                // Gap = Current Time - Time Target Was Here
                return (float)(currentSessionTime - targetTimeAtDist);
            }
        }

        /// <summary>
        /// Returns the relative time gap in seconds between two cars, IGNORING lap count.
        /// Calculates the gap as if both cars are on the same lap, based on physical track position.
        /// Positive value means 'subject' is BEHIND 'target' on the track map.
        /// Negative value means 'subject' is AHEAD of 'target' on the track map.
        /// </summary>
        public float GetTimeGapRelative(int subjectSlotId, int targetSlotId)
        {
            lock (@lock)
            {
                if (trackLength <= 0) return 0f;
                if (!carHistories.TryGetValue(subjectSlotId, out var subject) ||
                    !carHistories.TryGetValue(targetSlotId, out var target))
                {
                    return 0f;
                }

                double sDist = subject.LastKnownDistance;
                double tDist = target.LastKnownDistance;

                // Calculate position within the lap (0 to TrackLength)
                double sLapDist = sDist % trackLength;
                double tLapDist = tDist % trackLength;

                // Determine shortest arc on the circular track
                // Delta > 0 means Target is ahead in linear meters, but we need to check wrapping
                double delta = (tLapDist - sLapDist + trackLength) % trackLength;
                double halfTrack = trackLength / 2.0;

                if (delta < halfTrack)
                {
                    // CASE: Target is physically AHEAD (Subject is BEHIND)
                    // Example: T=1000, S=900. Delta=100.
                    // We want to know when Target was at Subject's physical position.

                    // Calculate the total distance in Target's history that corresponds to Subject's position
                    // It is Target's current TotalDist minus the physical gap
                    double lookupDist = tDist - delta;

                    double tTime = target.GetTimeAtDistance(lookupDist);

                    if (tTime <= 0) return 0f;

                    // Gap = Time(Now) - Time(Target was at S_Pos)
                    // Use Target's LastKnownTime to ensure we compare apples to apples (sim time)
                    return (float)(target.LastKnownTime - tTime);
                }
                else
                {
                    // CASE: Subject is physically AHEAD (Target is BEHIND)
                    // Example: T=100, S=Track-100. Delta = 200. 
                    // Or T=900, S=1000.
                    // We want to know when Subject was at Target's physical position.

                    double reverseDelta = (sLapDist - tLapDist + trackLength) % trackLength;
                    double lookupDist = sDist - reverseDelta;

                    double sTime = subject.GetTimeAtDistance(lookupDist);

                    if (sTime <= 0) return 0f;

                    // Gap is negative because Subject is ahead
                    return -(float)(subject.LastKnownTime - sTime);
                }
            }
        }

        private void OnDataUpdated(Shared data)
        {
            // 1. Get Global Simulation Time (High precision double)
            double currentSimTime = data.Player.GameSimulationTime;

            // 2. Validate Data
            if (data.NumCars <= 0 || data.LayoutLength <= 0) return;

            // Cache track length for relative calculations
            trackLength = data.LayoutLength;

            // 3. Update History for All Cars
            lock (@lock)
            {
                for (int i = 0; i < data.NumCars; i++)
                {
                    var driver = data.DriverData[i];
                    int slotId = driver.DriverInfo.SlotId;

                    // Calculate absolute total distance (Laps * TrackLength + DistanceIntoLap)
                    float totalDistance = (driver.CompletedLaps * trackLength) + driver.LapDistance;

                    if (!carHistories.TryGetValue(slotId, out CarHistory? value))
                    {
                        logger.LogDebug("Creating new CarHistory for SlotId {SlotId}, Name {Name}", slotId, driver.DriverInfo.Name.ToNullTerminatedString());
                        value = new CarHistory();
                        carHistories[slotId] = value;
                    }

                    value.RecordSnapshot(totalDistance, currentSimTime);
                }

                // 4. Periodic Cleanup (Pruning)
                if (currentSimTime - lastPruneTime > PruneIntervalSeconds)
                {
                    PruneHistories();
                    lastPruneTime = currentSimTime;
                }
            }
        }

        private void PruneHistories()
        {
            if (carHistories.Count == 0) return;

            // Find the distance of the car that is furthest back
            double minDistance = carHistories.Values.Min(x => x.LastKnownDistance);

            // Keep a buffer (e.g. 5000 meters or 1 lap) behind the last car
            double deleteThreshold = minDistance - 5000.0;

            foreach (var history in carHistories.Values)
            {
                history.PruneOldData(deleteThreshold);
            }
        }

        private volatile bool _disposed = false;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (sharedSource != null)
            {
                sharedSource.DataUpdated -= OnDataUpdated;
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Helper class to manage the history of a single car
    /// </summary>
    public class CarHistory
    {
        private struct TelemetrySnapshot
        {
            public float TotalDistance;
            public double SessionTime;
        }

        private readonly List<TelemetrySnapshot> history = new(10000);

        public double LastKnownDistance { get; private set; }
        public double LastKnownTime { get; private set; }

        public void RecordSnapshot(float totalDistance, double sessionTime)
        {
            LastKnownDistance = totalDistance;
            LastKnownTime = sessionTime;

            // Optimization: Only add if distance has increased (prevent adding data when sitting in pits or reversing)
            if (history.Count > 0 && totalDistance <= history[history.Count - 1].TotalDistance)
            {
                return;
            }

            history.Add(new TelemetrySnapshot
            {
                TotalDistance = totalDistance,
                SessionTime = sessionTime
            });
        }

        public double GetTimeAtDistance(double targetDistance)
        {
            if (history.Count < 2) return 0;

            // If the target distance is beyond our recorded history, return 0
            if (targetDistance > history[history.Count - 1].TotalDistance) return 0;

            // If the target distance is before our history starts (pruned), return 0
            if (targetDistance < history[0].TotalDistance) return 0;

            // Binary Search
            int index = BinarySearch(targetDistance);

            if (index == -1 || index >= history.Count - 1) return 0;

            var p1 = history[index];
            var p2 = history[index + 1];

            // Linear Interpolation
            double totalDistDiff = p2.TotalDistance - p1.TotalDistance;

            if (totalDistDiff < 0.0001) return p1.SessionTime;

            double fraction = (targetDistance - p1.TotalDistance) / totalDistDiff;
            double interpolatedTime = p1.SessionTime + (fraction * (p2.SessionTime - p1.SessionTime));

            return interpolatedTime;
        }

        public void PruneOldData(double thresholdDistance)
        {
            int index = history.FindIndex(x => x.TotalDistance > thresholdDistance);

            if (index > 0)
            {
                // Keep 1 point before the threshold to ensure interpolation still works 
                int safeRemoveCount = index - 1;
                if (safeRemoveCount > 0)
                {
                    history.RemoveRange(0, safeRemoveCount);
                }
            }
        }

        private int BinarySearch(double targetDist)
        {
            int left = 0;
            int right = history.Count - 1;
            int resultIndex = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (history[mid].TotalDistance <= targetDist)
                {
                    resultIndex = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return resultIndex;
        }
    }
}