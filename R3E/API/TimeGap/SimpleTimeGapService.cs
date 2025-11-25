using R3E.Data;

namespace R3E.API.TimeGap
{
    /// <summary>
    /// Simple implementation of ITimeGapService using instantaneous speed calculations.
    /// This is more accurate and easier to debug than the history-based interpolation method.
    /// </summary>
    public class SimpleTimeGapService : ITimeGapService, IDisposable
    {
        private readonly ILogger<SimpleTimeGapService> logger;
        private readonly ITelemetryService telemetryService;

        private float trackLength = 0;
        private TelemetryData? currentData;

        public SimpleTimeGapService(ILogger<SimpleTimeGapService> logger,
                                    ITelemetryService telemetryService)
        {
            this.logger = logger;
            this.telemetryService = telemetryService;

            this.telemetryService.DataUpdated += OnDataUpdated;
            this.telemetryService.SessionTypeChanged += OnSessionTypeChanged;

            this.logger.LogInformation("SimpleTimeGapService initialized");
        }

        private void OnSessionTypeChanged(TelemetryData data)
        {
            if (data.Raw.LayoutLength > 0)
            {
                trackLength = data.Raw.LayoutLength;
                logger.LogInformation("Session changed. Track length: {TrackLength:F1}m", trackLength);
            }
        }

        private void OnDataUpdated(TelemetryData data)
        {
            currentData = data;

            if (data.Raw.LayoutLength > 0)
            {
                trackLength = data.Raw.LayoutLength;
            }
        }

        public float GetTimeGapRelative(int subjectSlotId, int targetSlotId)
        {
            if (currentData == null || trackLength <= 0)
            {
                logger.LogDebug("GetTimeGapRelative: No data or invalid track length");
                return 0f;
            }

            var subject = FindDriverBySlotId(subjectSlotId);
            var target = FindDriverBySlotId(targetSlotId);

            if (!subject.HasValue || !target.HasValue)
            {
                logger.LogDebug("GetTimeGapRelative: Driver not found (subject={SubjectSlotId}, target={TargetSlotId})",
                    subjectSlotId, targetSlotId);
                return 0f;
            }

            // Calculate total distance traveled
            float subjectDistance = (subject.Value.CompletedLaps * trackLength) + subject.Value.LapDistance;
            float targetDistance = (target.Value.CompletedLaps * trackLength) + target.Value.LapDistance;

            // Use the simple calculator
            float gap = SimpleTimeGapCalculator.CalculateGapBySpeed(
                subjectDistance, subject.Value.CarSpeed,
                targetDistance, target.Value.CarSpeed,
                trackLength);

            return gap;
        }

        private DriverData? FindDriverBySlotId(int slotId)
        {
            if (currentData?.Raw.DriverData == null)
                return null;

            for (int i = 0; i < currentData.Raw.NumCars; i++)
            {
                var driver = currentData.Raw.DriverData[i];
                if (driver.DriverInfo.SlotId == slotId)
                    return driver;
            }

            return null;
        }

        public void Dispose()
        {
            telemetryService.DataUpdated -= OnDataUpdated;
            telemetryService.SessionTypeChanged -= OnSessionTypeChanged;
            GC.SuppressFinalize(this);
        }
    }
}
