using R3E.Core.Interfaces;
using R3E.Features.Sector;

namespace R3E.Core.Services
{
    /// <summary>
    /// Central event bus for cross-feature communication.
    /// Registered as a singleton - all services share the same instance.
    /// </summary>
    public class TelemetryEventBus : ITelemetryEventBus
    {
        // Sector events
        public event Action<SectorData, int>? SectorCompleted;

        // Fuel events
        public event Action<double>? FuelLevelCritical;
        public event Action<double>? LapFuelUsageCalculated;

        // Driver events
        public event Action<int, int>? PositionChanged;
        public event Action<int>? DriverEnteredPit;
        public event Action<int>? DriverExitedPit;

        // Publish methods
        public void PublishSectorCompleted(SectorData sectorData, int sectorIndex)
            => SectorCompleted?.Invoke(sectorData, sectorIndex);

        public void PublishFuelLevelCritical(double percentage)
            => FuelLevelCritical?.Invoke(percentage);

        public void PublishLapFuelUsageCalculated(double liters)
            => LapFuelUsageCalculated?.Invoke(liters);

        public void PublishPositionChanged(int slotId, int newPosition)
            => PositionChanged?.Invoke(slotId, newPosition);

        public void PublishDriverEnteredPit(int slotId)
            => DriverEnteredPit?.Invoke(slotId);

        public void PublishDriverExitedPit(int slotId)
            => DriverExitedPit?.Invoke(slotId);
    }
}
