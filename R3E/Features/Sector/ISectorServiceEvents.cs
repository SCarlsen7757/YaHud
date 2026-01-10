
namespace R3E.Features.Sector
{
    public interface ISectorServiceEvents
    {
        event Action<SectorData, int>? SectorCompleted;
        void InvokeSectorCompleted(SectorData sectorData, int sectorIndex);
    }
}