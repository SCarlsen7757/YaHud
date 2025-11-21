using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class DataPointService : IDisposable
    {
        private readonly ILogger<DataPointService> _logger;
        private readonly ISharedSource _sharedSource;

        // Dictionary to hold history for every car.
        // Key = SlotId (Unique ID for the driver in the session), NOT the array index.
        private readonly Dictionary<int, CarHistory> _carHistories = [];

        private double _lastPruneTime = 0;
        private const double PruneIntervalSeconds = 10.0; // Clean up data every 10 seconds

        public DataPointService(ILogger<DataPointService> logger, ISharedSource sharedSource)
        {
            _logger = logger;
            _sharedSource = sharedSource;
            _sharedSource.DataUpdated += OnDataUpdated;

            _logger.LogInformation("DataPointService initialized");
        }

        /// <summary>
        /// Returns the time gap in seconds between two cars.
        /// Positive value means 'subject' is BEHIND 'target'.
        /// </summary>
        public float GetTimeGap(int subjectSlotId, int targetSlotId)
        {
            // We need the subject's current location
            if (!_carHistories.TryGetValue(subjectSlotId, out var subjectHistory) ||
                !_carHistories.TryGetValue(targetSlotId, out var targetHistory))
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

        private void OnDataUpdated(Shared data)
        {
            // 1. Get Global Simulation Time (High precision double)
            // Using Player.GameSimulationTime is reliable for the "Current Time" of the frame.
            double currentSimTime = data.Player.GameSimulationTime;

            // 2. Validate Data
            if (data.NumCars <= 0 || data.LayoutLength <= 0) return;

            float trackLength = data.LayoutLength;

            // 3. Update History for All Cars
            // Note: We iterate only up to NumCars
            for (int i = 0; i < data.NumCars; i++)
            {
                var driver = data.DriverData[i];

                // CRITICAL: Use SlotId, because the DriverData array is sorted by Position
                // and drivers change indices when they overtake.
                int slotId = driver.DriverInfo.SlotId;

                // Calculate absolute total distance (Laps * TrackLength + DistanceIntoLap)
                // We use CompletedLaps (int) and LapDistance (float)
                float totalDistance = (driver.CompletedLaps * trackLength) + driver.LapDistance;

                if (!_carHistories.TryGetValue(slotId, out CarHistory? value))
                {
                    _logger.LogDebug("Creating new CarHistory for SlotId {SlotId}, Name {Name}", slotId, driver.DriverInfo.Name.ToNullTerminatedString());
                    value = new CarHistory();
                    _carHistories[slotId] = value;
                }

                value.RecordSnapshot(totalDistance, currentSimTime);
            }

            // 4. Periodic Cleanup (Pruning)
            // We don't want to do this every 16ms
            if (currentSimTime - _lastPruneTime > PruneIntervalSeconds)
            {
                PruneHistories();
                _lastPruneTime = currentSimTime;
            }
        }

        private void PruneHistories()
        {
            if (_carHistories.Count == 0) return;

            // Find the distance of the car that is furthest back
            double minDistance = _carHistories.Values.Min(x => x.LastKnownDistance);

            // Keep a buffer (e.g. 5000 meters or 1 lap) behind the last car
            // We delete anything older than that because no car will ever ask for it.
            double deleteThreshold = minDistance - 5000.0;

            foreach (var history in _carHistories.Values)
            {
                history.PruneOldData(deleteThreshold);
            }
        }

        private volatile bool _disposed = false;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_sharedSource != null)
            {
                _sharedSource.DataUpdated -= OnDataUpdated;
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

        private readonly List<TelemetrySnapshot> _history = new List<TelemetrySnapshot>(10000);

        public double LastKnownDistance { get; private set; }
        public double LastKnownTime { get; private set; }

        public void RecordSnapshot(float totalDistance, double sessionTime)
        {
            LastKnownDistance = totalDistance;
            LastKnownTime = sessionTime;

            // Optimization: Only add if distance has increased (prevent adding data when sitting in pits or reversing)
            // Also simple noise filter
            if (_history.Count > 0 && totalDistance <= _history[_history.Count - 1].TotalDistance)
            {
                return;
            }

            _history.Add(new TelemetrySnapshot
            {
                TotalDistance = totalDistance,
                SessionTime = sessionTime
            });
        }

        public double GetTimeAtDistance(double targetDistance)
        {
            if (_history.Count < 2) return 0;

            // If the target distance is beyond our recorded history (e.g. the chaser is actually AHEAD), return 0
            if (targetDistance > _history[_history.Count - 1].TotalDistance) return 0;

            // If the target distance is before our history starts (we deleted it), return 0
            if (targetDistance < _history[0].TotalDistance) return 0;

            // Binary Search to find the index
            int index = BinarySearch(targetDistance);

            if (index == -1 || index >= _history.Count - 1) return 0;

            var p1 = _history[index];
            var p2 = _history[index + 1];

            // Linear Interpolation
            // Formula: Time = T1 + (DistanceFraction * (T2 - T1))
            double totalDistDiff = p2.TotalDistance - p1.TotalDistance;

            // Prevent divide by zero
            if (totalDistDiff < 0.0001) return p1.SessionTime;

            double fraction = (targetDistance - p1.TotalDistance) / totalDistDiff;
            double interpolatedTime = p1.SessionTime + (fraction * (p2.SessionTime - p1.SessionTime));

            return interpolatedTime;
        }

        public void PruneOldData(double thresholdDistance)
        {
            // Find the last index that is smaller than threshold
            int removeCount = 0;

            // Simple check from start since we are removing from start
            // A binary search could be used here too for perf, but usually we only prune a few items at a time
            // unless the interval is huge. Let's use FindIndex for simplicity.

            int index = _history.FindIndex(x => x.TotalDistance > thresholdDistance);

            if (index > 0)
            {
                // Keep 1 point before the threshold to ensure interpolation still works 
                // for someone exactly at the threshold limit
                int safeRemoveCount = index - 1;
                if (safeRemoveCount > 0)
                {
                    _history.RemoveRange(0, safeRemoveCount);
                }
            }
        }

        private int BinarySearch(double targetDist)
        {
            int left = 0;
            int right = _history.Count - 1;
            int resultIndex = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (_history[mid].TotalDistance <= targetDist)
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