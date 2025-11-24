namespace R3E.API
{
    public interface ITelemetryService
    {
        event Action<TelemetryData>? DataUpdated;
        event Action<TelemetryData>? NewLap;
        event Action<TelemetryData>? NewSession;
        event Action<TelemetryData>? CarPositionChanged;
        event Action<TelemetryData>? TrackChanged;
        event Action<TelemetryData>? CarChanged;

        TelemetryData Data { get; }
    }
}
