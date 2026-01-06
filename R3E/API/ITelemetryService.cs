using R3E.API.Models;

namespace R3E.API
{
    public interface ITelemetryService
    {
        event Action<TelemetryData>? DataUpdated;
        event Action<int>? StartLightsChanged;
        event Action<TelemetryData>? NewLap;
        event Action<TelemetryData>? SessionTypeChanged;
        event Action<TelemetryData>? SessionPhaseChanged;
        event Action<TelemetryData>? CarPositionChanged;
        event Action<TelemetryData>? TrackChanged;
        event Action<TelemetryData>? CarChanged;
        /// <summary>
        /// The event is triggered when a sector of the track is completed. The int parameter is the 0-based index of the sector.
        /// </summary>
        event Action<TelemetryData, int>? SectorCompleted;

        TelemetryData Data { get; }
        SectorData SectorData { get; }
        RadarData RadarData { get; }
    }
}
