using R3E.Data;

namespace R3E.API.Models
{
    public class SectorData
    {
        public Shared Raw { get; internal set; } = new Shared();

        public int CurrentSectorIndexSelf {
            get {
                if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector1 && Raw.LapDistanceFraction < Raw.SectorStartFactors.Sector2) {
                    return 0;
                }
                else if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector2 && Raw.LapDistanceFraction < Raw.SectorStartFactors.Sector3) {
                    return 1;
                }
                else if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector3 && Raw.LapDistanceFraction < 1) {
                    return 2;
                }

                return -1;
            }
        } 
    }
}
