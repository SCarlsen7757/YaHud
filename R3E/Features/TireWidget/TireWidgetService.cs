using Microsoft.Extensions.Logging;
using R3E.Core.Interfaces;
using R3E.Core.Services;

namespace R3E.Features.TireWidget
{
    public class TireWidgetService : IDisposable
    {
        private readonly ILogger<TireWidgetService> logger;
        private readonly ITelemetryService telemetryService;

        public TireWidgetData TireWidgetData { get; }
        private readonly Lock sync = new();

        private int lastNumPitstopsPerformedFront = 0;
        private int lastNumPitstopsPerformedRear = 0;

        // Latches PitAction's tire-change bits (16/32) as soon as they're seen active during a
        // stop. NumPitstopsPerformed appears to increment once the stop completes, by which point
        // PitAction may have already cleared back to 0, so the bits can't reliably be read only at
        // the increment tick - they must be latched while the stop is in progress and consumed
        // (checked + cleared) when the counter increments.
        private bool frontTireChangeLatched;
        private bool rearTireChangeLatched;

        // These only ever track whether the front/rear tire set has been initialized at least
        // once; no time-based tire age is computed from them. GameSimulationTime can be -1 in
        // menus, so it must not be used as a sentinel/initialization value here.
        private bool isFrontTireSetInitialized;
        private bool isRearTireSetInitialized;

        private int tireSetStartLapFront = 0;
        private int tireSetStartLapRear = 0;

        public TireWidgetService(
            ILogger<TireWidgetService> logger,
            ITelemetryService telemetryService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));

            TireWidgetData = new TireWidgetData(telemetryService.Data);

            telemetryService.DataUpdated += OnDataUpdated;
            telemetryService.SessionTypeChanged += OnSessionTypeChanged;

            logger.LogInformation("TireWidgetService initialized");
        }

        private void OnSessionTypeChanged(TelemetryData data)
        {
            lock (sync)
            {
                logger.LogInformation("TireWidgetService: session changed - clearing state.");
                TireWidgetData.FrontTireAge = 0;
                TireWidgetData.RearTireAge = 0;
                TireWidgetData.FrontLeftTireTemp = 0;
                TireWidgetData.FrontRightTireTemp = 0;
                TireWidgetData.RearLeftTireTemp = 0;
                TireWidgetData.RearRightTireTemp = 0;
                TireWidgetData.TireWear = new Data.TireData<float> { FrontLeft = 0, FrontRight = 0, RearLeft = 0, RearRight = 0 };
            }

            lastNumPitstopsPerformedFront = 0;
            lastNumPitstopsPerformedRear = 0;

            isFrontTireSetInitialized = false;
            isRearTireSetInitialized = false;

            frontTireChangeLatched = false;
            rearTireChangeLatched = false;

            tireSetStartLapFront = 0;
            tireSetStartLapRear = 0;
        }

        private void OnDataUpdated(TelemetryData data)
        {
            UpdateTireAge(data);
            UpdateTireTemperatures(data);
            UpdateTireWear(data);
        }

        public void Dispose()
        {
            telemetryService.DataUpdated -= OnDataUpdated;
            telemetryService.SessionTypeChanged -= OnSessionTypeChanged;
            GC.SuppressFinalize(this);
        }

        private void UpdateTireAge(TelemetryData data)
        {
            // PitAction is -1 when not applicable; -1 is all-bits-set in two's complement, so a
            // raw bitmask check against it would spuriously match every bit. Only latch while it's
            // a valid (non-negative) bitmask.
            if (data.Raw.PitAction >= 0)
            {
                // PitAction bit 4 (16) indicates front tires were changed
                if ((data.Raw.PitAction & 16) != 0) frontTireChangeLatched = true;
                // PitAction bit 5 (32) indicates rear tires were changed
                if ((data.Raw.PitAction & 32) != 0) rearTireChangeLatched = true;
            }

            if (data.Raw.NumPitstopsPerformed > lastNumPitstopsPerformedFront)
            {
                if (frontTireChangeLatched)
                {
                    ResetFrontTireAge();
                }
                frontTireChangeLatched = false;
                lastNumPitstopsPerformedFront = data.Raw.NumPitstopsPerformed;
            }

            if (data.Raw.NumPitstopsPerformed > lastNumPitstopsPerformedRear)
            {
                if (rearTireChangeLatched)
                {
                    ResetRearTireAge();
                }
                rearTireChangeLatched = false;
                lastNumPitstopsPerformedRear = data.Raw.NumPitstopsPerformed;
            }

            if (!isFrontTireSetInitialized)
            {
                ResetFrontTireAge();
            }

            if (!isRearTireSetInitialized)
            {
                ResetRearTireAge();
            }

            // Lock ensures FrontTireAge and RearTireAge are updated atomically together
            lock (sync)
            {
                TireWidgetData.FrontTireAge = data.Raw.CompletedLaps - tireSetStartLapFront;
                TireWidgetData.RearTireAge = data.Raw.CompletedLaps - tireSetStartLapRear;
            }

        }

        private void ResetFrontTireAge()
        {
            tireSetStartLapFront = telemetryService.Data.Raw.CompletedLaps;
            isFrontTireSetInitialized = true;
        }

        private void ResetRearTireAge()
        {
            tireSetStartLapRear = telemetryService.Data.Raw.CompletedLaps;
            isRearTireSetInitialized = true;
        }

        private void UpdateTireTemperatures(TelemetryData data)
        {
            var avgTireTempFrontLeft = (data.Raw.TireTemp.FrontLeft.CurrentTemp.Left + data.Raw.TireTemp.FrontLeft.CurrentTemp.Right + data.Raw.TireTemp.FrontLeft.CurrentTemp.Center) / 3.0f;
            var avgTireTempFrontRight = (data.Raw.TireTemp.FrontRight.CurrentTemp.Left + data.Raw.TireTemp.FrontRight.CurrentTemp.Right + data.Raw.TireTemp.FrontRight.CurrentTemp.Center) / 3.0f;
            var avgTireTempRearLeft = (data.Raw.TireTemp.RearLeft.CurrentTemp.Left + data.Raw.TireTemp.RearLeft.CurrentTemp.Right + data.Raw.TireTemp.RearLeft.CurrentTemp.Center) / 3.0f;
            var avgTireTempRearRight = (data.Raw.TireTemp.RearRight.CurrentTemp.Left + data.Raw.TireTemp.RearRight.CurrentTemp.Right + data.Raw.TireTemp.RearRight.CurrentTemp.Center) / 3.0f;

            lock (sync)
            {
                TireWidgetData.FrontLeftTireTemp = avgTireTempFrontLeft;
                TireWidgetData.FrontRightTireTemp = avgTireTempFrontRight;
                TireWidgetData.RearLeftTireTemp = avgTireTempRearLeft;
                TireWidgetData.RearRightTireTemp = avgTireTempRearRight;
            }
        }

        private void UpdateTireWear(TelemetryData data)
        {
            lock (sync)
            {
                TireWidgetData.TireWear = new Data.TireData<float> {
                    FrontLeft = ConvertTireWearToPercentage(data.Raw.TireWear.FrontLeft),
                    FrontRight = ConvertTireWearToPercentage(data.Raw.TireWear.FrontRight),
                    RearLeft = ConvertTireWearToPercentage(data.Raw.TireWear.RearLeft),
                    RearRight = ConvertTireWearToPercentage(data.Raw.TireWear.RearRight)
                };

            }
        }

        private float ConvertTireWearToPercentage(float wear)
        {
            // Map sentinel / negative values (e.g., -1 for N/A) to 0%
            if (wear < 0f)
            {
                return 0f;
            }
            var percentage = wear * 100f;
            if (percentage < 0f)
            {
                percentage = 0f;
            }
            else if (percentage > 100f)
            {
                percentage = 100f;
            }
            return percentage;
        }
    }
}
