using R3E.Extensions;

namespace R3E.API.TimeGap
{
    public class AdvancedTimeGapService : ITimeGapService, IDisposable
    {
        private readonly ILogger<AdvancedTimeGapService> logger;
        private readonly ITelemetryService telemetryService;

        // Dictionary to hold history for every car.
        private readonly Dictionary<int, CarHistory> carHistories = [];
        private readonly Lock @lock = new();

        private float trackLength = 0;

        public AdvancedTimeGapService(ILogger<AdvancedTimeGapService> logger,
                              ITelemetryService telemetryService)
        {
            this.logger = logger;
            this.telemetryService = telemetryService;

            this.telemetryService.DataUpdated += OnDataUpdated;
            this.telemetryService.SessionTypeChanged += OnSessionTypeChanged;

            this.logger.LogInformation("TimeGapService initialized");
        }

        private void OnSessionTypeChanged(TelemetryData data)
        {
            lock (@lock)
            {
                logger.LogInformation("New Session. Clearing Car Histories.");
                carHistories.Clear();

                if (data.Raw.LayoutLength > 0)
                {
                    trackLength = data.Raw.LayoutLength;
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
                    return 0f;
                }

                return CalculateRelativeGap(subject, target, trackLength);
            }
        }

        private float CalculateRelativeGap(CarHistory subject, CarHistory target, double trackLen)
        {
            double sDist = subject.LastKnownDistance;
            double tDist = target.LastKnownDistance;

            // Normalize positions to 0..TrackLength range
            double sLapDist = sDist % trackLen;
            double tLapDist = tDist % trackLen;

            // Calculate shortest arc on the circle
            double delta = (tLapDist - sLapDist + trackLen) % trackLen;
            double halfTrack = trackLen / 2.0;

            // DIAGNOSTIC: Log the calculation for debugging
            logger.LogDebug("Gap Calc: sDist={SDist:F1}m, tDist={TDist:F1}m, sLap={SLap:F1}m, tLap={TLap:F1}m, delta={Delta:F1}m",
                sDist, tDist, sLapDist, tLapDist, delta);

            if (delta < halfTrack)
            {
                // Target is physically AHEAD on track
                // Question: "When target was at subject's position, what was their time?"
                double lookupDist = sDist;  // We want to know target's time when they were at subject's current position
                double tTime = target.GetTimeAtDistance(lookupDist);

                if (tTime <= 0)
                {
                    logger.LogDebug("Target ahead but no history at {LookupDist:F1}m", lookupDist);
                    return 0f;
                }

                // Positive gap = target is ahead
                float gap = (float)(target.LastKnownTime - tTime);
                logger.LogDebug("Target AHEAD: gap={Gap:F3}s (tNow={TNow:F3}s, tThen={TThen:F3}s)",
                    gap, target.LastKnownTime, tTime);
                return gap;
            }
            else
            {
                // Subject is physically AHEAD on track
                // Question: "When subject was at target's position, what was their time?"
                double lookupDist = tDist;  // We want to know subject's time when they were at target's current position
                double sTime = subject.GetTimeAtDistance(lookupDist);

                if (sTime <= 0)
                {
                    logger.LogDebug("Subject ahead but no history at {LookupDist:F1}m", lookupDist);
                    return 0f;
                }

                // Negative gap = subject is ahead (target is behind)
                float gap = -(float)(subject.LastKnownTime - sTime);
                logger.LogDebug("Subject AHEAD: gap={Gap:F3}s (sNow={SNow:F3}s, sThen={SThen:F3}s)",
                    gap, subject.LastKnownTime, sTime);
                return gap;
            }
        }

        private void OnDataUpdated(TelemetryData data)
        {
            if (data.Raw.NumCars <= 0 || data.Raw.LayoutLength <= 0) return;
            var sessionPhase = (Constant.SessionPhase)data.Raw.SessionPhase;
            if (sessionPhase != Constant.SessionPhase.Green) return;

            trackLength = data.Raw.LayoutLength;
            double currentSimTime = data.Raw.Player.GameSimulationTime;

            lock (@lock)
            {
                for (int i = 0; i < data.Raw.NumCars; i++)
                {
                    var driver = data.Raw.DriverData[i];
                    int slotId = driver.DriverInfo.SlotId;

                    if (slotId < 0) continue;

                    var finishStatus = (Constant.FinishStatus)driver.FinishStatus;
                    if (finishStatus != Constant.FinishStatus.None)
                    {
                        if (carHistories.ContainsKey(driver.DriverInfo.SlotId))
                        {
                            logger.LogInformation("Removing CarHistory for SlotId {SlotId}, {Name} due to finish status: {FinishStatus}",
                                slotId,
                                driver.DriverInfo.Name.ToNullTerminatedString(),
                                finishStatus);
                            carHistories.Remove(slotId);
                        }
                        continue;
                    }

                    float totalDistance = (driver.CompletedLaps * trackLength) + driver.LapDistance;

                    if (!carHistories.TryGetValue(slotId, out CarHistory? history))
                    {
                        logger.LogDebug("Creating CarHistory for SlotId {SlotId}, {Name}", slotId, driver.DriverInfo.Name.ToNullTerminatedString());
                        history = new CarHistory();
                        carHistories[slotId] = history;
                    }

                    history.RecordSnapshot(totalDistance, currentSimTime, driver.CompletedLaps, trackLength);
                }
            }
        }

        public void Dispose()
        {
            telemetryService.DataUpdated -= OnDataUpdated;
            telemetryService.SessionTypeChanged -= OnSessionTypeChanged;
            GC.SuppressFinalize(this);
        }
    }

    public class CarHistory
    {
        private readonly record struct TelemetrySnapshot(float TotalDistance, double SessionTime);

        // Sufficient for ~3-4 minutes of data at 60Hz without resizing.
        // Calculation: 240 seconds (4 minutes) × 60 updates/sec = 14.400 data points.
        // Data size is 12 bytes (TotalDistance = 4 + SessionTime = 8) x 15000 = 180kB.
        // 12 Drivers x 180kB = 2,16MB if the lap time is less than 4 minutes.
        private readonly List<TelemetrySnapshot> history = new(15000);
        private int lastLapNumber = -1;

        public double LastKnownDistance { get; private set; }
        public double LastKnownTime { get; private set; }

        public string GetDiagnosticInfo(int slotId)
        {
            return $"Slot {slotId}: {history.Count} points, " +
                   $"LastDist={LastKnownDistance:F1}m, " +
                   $"LastTime={LastKnownTime:F3}s, " +
                   $"Range=[{(history.Count > 0 ? history[0].TotalDistance : 0):F1}m to {(history.Count > 0 ? history[^1].TotalDistance : 0):F1}m]";
        }

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