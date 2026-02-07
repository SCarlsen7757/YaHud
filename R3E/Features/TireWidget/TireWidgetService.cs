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

        private double tireSetStartTimeFront = 0;
        private double tireSetStartTimeRear = 0;

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
            GC.SuppressFinalize(this);
        }

        private void UpdateTireAge(TelemetryData data)
        {
            if (data.Raw.NumPitstopsPerformed > lastNumPitstopsPerformedFront)
            {
                // PitAction bit 4 (16) indicates front tires were changed
                bool tiresChanged = (data.Raw.PitAction & 16) != 0;
                if (tiresChanged)
                {
                    ResetFrontTireAge();
                }
                lastNumPitstopsPerformedFront = data.Raw.NumPitstopsPerformed;
            }

            if (data.Raw.NumPitstopsPerformed > lastNumPitstopsPerformedRear)
            {
                // PitAction bit 5 (32) indicates rear tires were changed
                bool tiresChanged = (data.Raw.PitAction & 32) != 0;
                if (tiresChanged)
                {
                    ResetRearTireAge();
                }
                lastNumPitstopsPerformedRear = data.Raw.NumPitstopsPerformed;
            }

            if (tireSetStartTimeFront == 0)
            {
                ResetFrontTireAge();
            }

            if (tireSetStartTimeRear == 0)
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
            tireSetStartTimeFront = telemetryService.Data.Raw.Player.GameSimulationTime;
        }

        private void ResetRearTireAge()
        {
            tireSetStartLapRear = telemetryService.Data.Raw.CompletedLaps;
            tireSetStartTimeRear = telemetryService.Data.Raw.Player.GameSimulationTime;
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
                    FrontLeft = data.Raw.TireWear.FrontLeft * 100f, // Convert to percentage
                    FrontRight = data.Raw.TireWear.FrontRight * 100f, // Convert to percentage
                    RearLeft = data.Raw.TireWear.RearLeft * 100f, // Convert to percentage
                    RearRight = data.Raw.TireWear.RearRight * 100f // Convert to percentage
                };

            }
        }
    }
}
