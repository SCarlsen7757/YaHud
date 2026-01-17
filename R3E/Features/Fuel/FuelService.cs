using R3E.Core.Interfaces;
using R3E.Core.Services;

namespace R3E.Features.Fuel
{
    /// <summary>
    /// Service responsible for fuel-related calculations and state management.
    /// Subscribes to telemetry events and maintains fuel usage history.
    /// </summary>
    public class FuelService : IDisposable
    {
        private readonly ITelemetryService telemetry;
        private readonly ILogger<FuelService> logger;
        private readonly TelemetryData telemetryData;

        private double oldFuelRemaining;
        private bool disposed;

        /// <summary>
        /// Current fuel data with computed properties.
        /// Single instance created once and reused - not recreated on every update.
        /// </summary>
        public FuelData Data { get; }

        public FuelService(
            ITelemetryService telemetry,
            ILogger<FuelService> logger)
        {
            this.telemetry = telemetry;
            this.logger = logger;

            // Store reference to TelemetryData once
            telemetryData = telemetry.Data;

            // Create FuelData once - it will read from telemetryData.Raw
            Data = new FuelData(telemetryData);

            // Subscribe to events
            telemetry.NewLap += OnNewLap;
            telemetry.SessionPhaseChanged += OnSessionPhaseChanged;
        }

        private void OnSessionPhaseChanged(TelemetryData data)
        {
            var phase = (Constant.SessionPhase)telemetryData.Raw.SessionPhase;

            if (phase == Constant.SessionPhase.Countdown || phase == Constant.SessionPhase.Formation)
            {
                oldFuelRemaining = telemetryData.Raw.FuelLeft;
                logger.LogInformation("Fuel tracking initialized: {FuelLeft:F2}L", oldFuelRemaining);
            }
        }

        private void OnNewLap(TelemetryData data)
        {
            var currentFuel = telemetryData.Raw.FuelLeft;
            var fuelUsed = oldFuelRemaining - currentFuel;

            Data.LastLapFuelUsage = fuelUsed;
            oldFuelRemaining = currentFuel;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            telemetry.NewLap -= OnNewLap;
            telemetry.SessionPhaseChanged -= OnSessionPhaseChanged;

            GC.SuppressFinalize(this);
        }
    }
}
