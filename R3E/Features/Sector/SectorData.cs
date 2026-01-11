using R3E.Core.Services;
using R3E.Data;

namespace R3E.Features.Sector
{
    /// <summary>
    /// Contains calculations done over the raw data, to provide properties around the sectors
    /// </summary>
    public class SectorData
    {
        public SectorData(TelemetryData telemetryData)
        {
            this.telemetryData = telemetryData;
        }

        private readonly TelemetryData telemetryData;

        private Shared Raw => telemetryData.Raw;

        /// <summary>
        /// Returns a 0-based sector index, or -1
        /// </summary>
        public int CurrentSectorIndexSelf
        {
            get
            {
                if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector1 && Raw.LapDistanceFraction < Raw.SectorStartFactors.Sector2)
                {
                    return 0;
                }
                else if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector2 && Raw.LapDistanceFraction < Raw.SectorStartFactors.Sector3)
                {
                    return 1;
                }
                else if (Raw.LapDistanceFraction >= Raw.SectorStartFactors.Sector3 && Raw.LapDistanceFraction < 1)
                {
                    return 2;
                }

                return -1;
            }
        }
    }
}
