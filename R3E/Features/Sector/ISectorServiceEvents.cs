
namespace R3E.Features.Sector
{
    public interface ISectorServiceEvents
    {
        event Action<SectorData, int>? SectorCompleted;
        void PublishSectorCompleted(SectorData sectorData, int sectorIndex);
    }
}