using R3E.Core.Services;

namespace R3E.Core.Interfaces
{
    public interface ITelemetryService
    {
        event Action<TelemetryData>? DataUpdated;
        event Action<int>? StartLightsChanged;
        event Action<TelemetryData>? NewLap;
        event Action<TelemetryData>? SessionTypeChanged;

        /// <summary>
        /// Raised when accumulated telemetry state must be discarded: the session type changed, or
        /// simulation ticks went backwards (a session restart, RaceRoom's own replay, or a replay
        /// rewind). Consumers that carry state across frames should reset on this rather than on
        /// <see cref="SessionTypeChanged"/>.
        /// </summary>
        event Action<TelemetryData>? TelemetryReset;

        event Action<TelemetryData>? SessionPhaseChanged;
        event Action<TelemetryData>? CarPositionChanged;
        event Action<TelemetryData>? TrackChanged;
        event Action<TelemetryData>? CarChanged;

        TelemetryData Data { get; }
    }
}
