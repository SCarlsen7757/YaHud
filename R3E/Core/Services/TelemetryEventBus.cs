using R3E.Core.Interfaces;
using R3E.Features.Sector;

namespace R3E.Core.Services
{
    /// <summary>
    /// Central event bus for cross-feature communication.
    /// </summary>
    public class TelemetryEventBus : ITelemetryEventBus
    {
        public event Action<SectorData, int>? SectorCompleted;

        // Notify methods
        public void InvokeSectorCompleted(SectorData sectorData, int sectorIndex)
            => SectorCompleted?.Invoke(sectorData, sectorIndex);
    }
}
