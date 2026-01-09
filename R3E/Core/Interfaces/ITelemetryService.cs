using R3E.Core.Services;

namespace R3E.Core.Interfaces
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

        TelemetryData Data { get; }
    }
}
