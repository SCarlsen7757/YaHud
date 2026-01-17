using R3E.Features.Sector;

namespace R3E.Core.Interfaces
{
    public interface ITelemetryEventBus
    {
        event Action<SectorData, int>? SectorCompleted;
        void InvokeSectorCompleted(SectorData sectorData, int sectorIndex);
    }
}
