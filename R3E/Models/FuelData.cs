using R3E.API;
using R3E.Data;

namespace R3E.Models;

/// <summary>
/// Represents fuel-related telemetry for a session.
/// Provides calculations for fuel remaining, fuel per lap, time and lap estimates, and fuel to add.
/// </summary>
public class FuelData: IDisposable
{
    private volatile bool disposed;
    private readonly Shared telemetryData;
    private readonly ITelemetryService telemetryService;
    private double oldFuelRemaining;
    /// <summary>
    /// Fuel used in the last completed lap (liters or relevant unit from telemetry).
    /// </summary>
    public double LastLapFuelUsage { get; set; } 
    
    /// <summary>
    /// Initializes a new instance of <see cref="FuelData"/>.
    /// Subscribes to telemetry service events to update fuel calculations on lap and session changes.
    /// </summary>
    public FuelData(Shared telemetryData, ITelemetryService telemetryService)
    {
        this.telemetryData = telemetryData;
        this.telemetryService = telemetryService;
        telemetryService.NewLap += TelemetryServiceOnNewLap;
        telemetryService.SessionPhaseChanged += TelemetryServiceOnSessionPhaseChanged;
    }

    /// <summary>
    /// Called when session phase changes.
    /// Resets old fuel remaining at the start of the session countdown.
    /// </summary>
    private void TelemetryServiceOnSessionPhaseChanged(TelemetryData obj)
    {
        if ((Constant.SessionPhase)telemetryData.SessionPhase == Constant.SessionPhase.Countdown)
        {
            oldFuelRemaining = telemetryData.FuelLeft;
        }
    }

    /// <summary>
    /// Called when a new lap starts.
    /// Updates <see cref="LastLapFuelUsage"/>.
    /// </summary>
    private void TelemetryServiceOnNewLap(TelemetryData obj)
    {
        //Everytime we pass race line NumberOfLaps should update so we use this as a trigger to calculate the remaining variables 
        LastLapFuelUsage = oldFuelRemaining - telemetryData.FuelLeft;
        oldFuelRemaining = telemetryData.FuelLeft;
    }

    /// <summary>
    /// Current fuel left in the tank (liters or telemetry unit). Returns 0 if negative.
    /// </summary>
    public double FuelLeft => telemetryData.FuelLeft <= 0 ? 0.0f : telemetryData.FuelLeft;

    /// <summary>
    /// Estimated time left in the session based on fuel and lap time (seconds).
    /// Returns 0 if lap time or fuel per lap is unavailable.
    /// </summary>
    public double TimeEstimatedLeft => telemetryData.LapTimeBestSelf <= 0 || telemetryData.FuelPerLap <= 0 ? 0
        : (telemetryData.FuelLeft / telemetryData.FuelPerLap) * telemetryData.LapTimeBestSelf;
    
    /// <summary>
    /// Fuel remaining as a percentage of total capacity.
    /// Returns 0 if capacity is zero.
    /// </summary>
    public double FuelRemainingPercentage => telemetryData.FuelCapacity <= 0 ? 0.0f : (telemetryData.FuelLeft / telemetryData.FuelCapacity) * 100;

    /// <summary>
    /// Estimated laps remaining with current fuel.
    /// </summary>
    public double LapsEstimatedLeft => telemetryData.FuelPerLap <= 0 ? 0.0f : telemetryData.FuelLeft / telemetryData.FuelPerLap;

    /// <summary>
    /// Fuel consumption per lap.
    /// </summary>
    public double FuelPerLap => telemetryData.FuelPerLap <= 0 ? 0.0f : telemetryData.FuelPerLap;

    /// <summary>
    /// Fuel required to reach session end based on session type.
    /// Assumes even fuel usage per lap or per time unit.
    /// </summary>
    public double FuelToEnd
    {
        get
        {
            return (Constant.SessionLengthFormat)telemetryData.SessionLengthFormat switch
            {
                Constant.SessionLengthFormat.Unavailable => double.NaN //Nothing to calculate
                ,
                // TimeBased
                Constant.SessionLengthFormat.TimeBased => (telemetryData.SessionTimeRemaining /
                                                           telemetryData.LapTimeBestSelf) * telemetryData.FuelPerLap,
                // LapBased
                Constant.SessionLengthFormat.LapBased => telemetryData.NumberOfLaps / telemetryData.FuelPerLap,
                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                Constant.SessionLengthFormat.TimeAndLapBased => (telemetryData.NumberOfLaps + 1) /
                                                                telemetryData.FuelPerLap,
                _ => double.NaN
            };
        }
    }

    /// <summary>
    /// Fuel needed to add to reach session end, constrained by tank capacity.
    /// </summary>
    public double FuelToAdd
    {
        get
        {
            return (Constant.SessionLengthFormat)telemetryData.SessionLengthFormat switch
            {
                Constant.SessionLengthFormat.Unavailable => double.NaN //Nothing to calculate
                ,
                // TimeBased
                Constant.SessionLengthFormat.TimeBased => Math.Min(FuelToEnd - telemetryData.FuelLeft,
                    telemetryData.FuelCapacity - telemetryData.FuelLeft),
                // LapBased
                Constant.SessionLengthFormat.LapBased => Math.Min(FuelToEnd - telemetryData.FuelLeft,
                    telemetryData.FuelCapacity - telemetryData.FuelLeft),
                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                Constant.SessionLengthFormat.TimeAndLapBased => Math.Min(FuelToEnd - telemetryData.FuelLeft,
                    telemetryData.FuelCapacity - telemetryData.FuelLeft),
                _ => double.NaN
            };
        } 
    }

    /// <summary>
    /// Releases resources and unsubscribes from telemetry events.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        telemetryService.NewLap -= TelemetryServiceOnNewLap;
        telemetryService.SessionPhaseChanged -= TelemetryServiceOnSessionPhaseChanged;
        GC.SuppressFinalize(this); 
    }
}