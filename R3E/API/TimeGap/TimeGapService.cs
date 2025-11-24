using R3E.Data;

namespace R3E.API.TimeGap
{
    public class TimeGapService : IDisposable
    {
        private readonly ILogger<TimeGapService> logger;
        private readonly ISharedSource sharedSource;

        // Dictionary to hold history for every car.
        private readonly Dictionary<int, CarHistory> carHistories = [];
        private readonly Lock @lock = new();

        private float trackLength = 0;

        public TimeGapService(ILogger<TimeGapService> logger, ISharedSource sharedSource)
        {
            this.logger = logger;
            this.sharedSource = sharedSource;

            this.sharedSource.DataUpdated += OnDataUpdated;
            //this.sharedSource.NewSession += OnNewSession;

            this.logger.LogInformation("TimeGapService initialized");
        }

        private void OnNewSession(Shared data)
        {
            lock (@lock)
            {
                logger.LogInformation("New Session. Clearing Car Histories.");
                carHistories.Clear();

                if (data.LayoutLength > 0)
                {
                    trackLength = data.LayoutLength;
                }
            }
        }

        public float GetTimeGapRelative(int subjectSlotId, int targetSlotId)
        {
            lock (@lock)
            {
                if (trackLength <= 0) return 0f;
                if (!carHistories.TryGetValue(subjectSlotId, out var subject) ||
                    !carHistories.TryGetValue(targetSlotId, out var target))
                {
                    // Optional: Log warning only periodically to avoid spamming
                    return 0f;
                }

                return CalculateRelativeGap(subject, target, trackLength);
            }
        }

        private static float CalculateRelativeGap(CarHistory subject, CarHistory target, double trackLen)
        {
            double sDist = subject.LastKnownDistance;
            double tDist = target.LastKnownDistance;

            // Normalize positions to 0..TrackLength range
            double sLapDist = sDist % trackLen;
            double tLapDist = tDist % trackLen;

            // Calculate shortest arc on the circle
            double delta = (tLapDist - sLapDist + trackLen) % trackLen;
            double halfTrack = trackLen / 2.0;

            if (delta < halfTrack)
            {
                // Target is physically AHEAD
                double lookupDist = tDist - delta;
                double tTime = target.GetTimeAtDistance(lookupDist);

                if (tTime <= 0) return 0f;
                return (float)(target.LastKnownTime - tTime);
            }
            else
            {
                // Subject is physically AHEAD
                double reverseDelta = (sLapDist - tLapDist + trackLen) % trackLen;
                double lookupDist = sDist - reverseDelta;
                double sTime = subject.GetTimeAtDistance(lookupDist);

                if (sTime <= 0) return 0f;
                return -(float)(subject.LastKnownTime - sTime);
            }
        }

        private void OnDataUpdated(Shared data)
        {
            if (data.NumCars <= 0 || data.LayoutLength <= 0) return;

            trackLength = data.LayoutLength;
            double currentSimTime = data.Player.GameSimulationTime;

            lock (@lock)
            {
                for (int i = 0; i < data.NumCars; i++)
                {
                    var driver = data.DriverData[i];
                    int slotId = driver.DriverInfo.SlotId;

                    if (slotId < 0) continue;

                    if (driver.FinishStatus == (int)Constant.FinishStatus.DNF ||
                        driver.FinishStatus == (int)Constant.FinishStatus.DQ ||
                        driver.FinishStatus == (int)Constant.FinishStatus.DNS)
                    {
                        carHistories.Remove(slotId);
                        continue;
                    }

                    float totalDistance = (driver.CompletedLaps * trackLength) + driver.LapDistance;

                    if (!carHistories.TryGetValue(slotId, out CarHistory? history))
                    {
                        history = new CarHistory();
                        carHistories[slotId] = history;
                    }

                    history.RecordSnapshot(totalDistance, currentSimTime, driver.CompletedLaps, trackLength);
                }
            }
        }

        public void Dispose()
        {
            if (sharedSource != null)
            {
                sharedSource.DataUpdated -= OnDataUpdated;
                //sharedSource.NewSession -= OnNewSession;
            }
            GC.SuppressFinalize(this);
        }
    }

    public class CarHistory
    {
        private readonly record struct TelemetrySnapshot(float TotalDistance, double SessionTime);

        // Sufficient for ~3-4 minutes of data at 60Hz without resizing.
        // Calculation: 240 seconds (4 minutes) × 60 updates/sec = 14.400 data points.
        // Data size is 12 bytes (TotalDistance = 4 + SessionTime = 8) x 15000 = 180kB.
        // 12 Drivers x 180kB = 2,16MB if the lap time is less then 4 minutes.
        private readonly List<TelemetrySnapshot> history = new(15000);
        private int lastLapNumber = -1;

        public double LastKnownDistance { get; private set; }
        public double LastKnownTime { get; private set; }

        public void RecordSnapshot(float totalDistance, double sessionTime, int lapNumber, float trackLength)
        {
            // 1. Detect Hard Reset (Session restart where lap number actually drops)
            bool isSessionReset = lastLapNumber != -1 && lapNumber < lastLapNumber;

            // 2. Detect Teleport (Return to Garage)
            // Even if LapNumber is same, if TotalDistance drops significantly (e.g. > 50m),
            // it means the car teleported backwards. We must clear history to resume tracking.
            bool isTeleport = false;
            if (!isSessionReset && history.Count > 0)
            {
                float lastDist = history[^1].TotalDistance;
                // 50 meters threshold to distinguish between reversing slightly and teleporting
                if (totalDistance < lastDist - 50.0f)
                {
                    isTeleport = true;
                }
            }

            if (isSessionReset || isTeleport)
            {
                history.Clear();
            }

            lastLapNumber = lapNumber;

            // 3. Normalize Storage (The "One Lap" Logic)
            if (trackLength > 0)
            {
                double deleteThreshold = totalDistance - (trackLength * 1.2);
                PruneOldData(deleteThreshold);
            }

            // 4. Record Data
            // We only record if we are moving forward relative to our last recorded point.
            // If we just cleared history (isTeleport), history.Count is 0, so we WILL record.
            bool isMovingForward = history.Count == 0 || totalDistance > history[^1].TotalDistance;

            if (isMovingForward)
            {
                history.Add(new TelemetrySnapshot(totalDistance, sessionTime));
                LastKnownDistance = totalDistance;
                LastKnownTime = sessionTime;
            }
            else
            {
                // If sitting still in pits or on grid, just update the time
                LastKnownTime = sessionTime;
            }
        }

        public double GetTimeAtDistance(double targetDistance)
        {
            if (history.Count < 2) return 0;

            // If requested distance is pruned, we can't calculate
            if (targetDistance < history[0].TotalDistance) return 0;

            int idx = BinarySearch(targetDistance);
            if (idx == -1 || idx >= history.Count - 1) return 0;

            var p1 = history[idx];
            var p2 = history[idx + 1];

            double distDiff = p2.TotalDistance - p1.TotalDistance;
            if (distDiff < 0.001) return p1.SessionTime;

            double fraction = (targetDistance - p1.TotalDistance) / distDiff;
            return p1.SessionTime + (fraction * (p2.SessionTime - p1.SessionTime));
        }

        private void PruneOldData(double threshold)
        {
            int removeCount = 0;
            // Optimization: Check from the start, stop as soon as we find a point inside the window
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].TotalDistance > threshold)
                {
                    // Keep one point BEFORE the threshold for interpolation
                    removeCount = i > 0 ? i - 1 : 0;
                    break;
                }
            }

            if (removeCount > 0)
            {
                history.RemoveRange(0, removeCount);
            }
        }

        private int BinarySearch(double targetDist)
        {
            int left = 0, right = history.Count - 1, res = -1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (history[mid].TotalDistance <= targetDist)
                {
                    res = mid;
                    left = mid + 1;
                }
                else right = mid - 1;
            }
            return res;
        }
    }
}