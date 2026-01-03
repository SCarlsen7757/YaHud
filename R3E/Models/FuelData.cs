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
    public Shared TelemetryData { get; set; }
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
        TelemetryData = telemetryData;
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
        if ((Constant.SessionPhase)TelemetryData.SessionPhase == Constant.SessionPhase.Countdown)
        {
            oldFuelRemaining = TelemetryData.FuelLeft;
        }
    }

    /// <summary>
    /// Called when a new lap starts.
    /// Updates <see cref="LastLapFuelUsage"/>.
    /// </summary>
    private void TelemetryServiceOnNewLap(TelemetryData obj)
    {
        // Every time we pass race line NumberOfLaps should update so we use this as a trigger to calculate the remaining variables 
        LastLapFuelUsage = oldFuelRemaining - TelemetryData.FuelLeft;
        oldFuelRemaining = TelemetryData.FuelLeft;
    }

    /// <summary>
    /// Current fuel left in the tank (liters or telemetry unit). Returns 0 if negative.
    /// </summary>
    public double FuelLeft => TelemetryData.FuelLeft <= 0 ? 0.0f : TelemetryData.FuelLeft;

    /// <summary>
    /// Estimated time left in the session based on fuel and lap time (seconds).
    /// Returns 0 if lap time or fuel per lap is unavailable.
    /// </summary>
    public double TimeEstimatedLeft => TelemetryData.LapTimeBestSelf <= 0 || TelemetryData.FuelPerLap <= 0 ? 0
        : (TelemetryData.FuelLeft / TelemetryData.FuelPerLap) * TelemetryData.LapTimeBestSelf;
    
    /// <summary>
    /// Fuel remaining as a percentage of total capacity.
    /// Returns 0 if capacity is zero.
    /// </summary>
    public double FuelRemainingPercentage => TelemetryData.FuelCapacity <= 0 ? 0.0f : (TelemetryData.FuelLeft / TelemetryData.FuelCapacity) * 100;

    /// <summary>
    /// Estimated laps remaining with current fuel.
    /// </summary>
    public double LapsEstimatedLeft => TelemetryData.FuelPerLap <= 0 ? 0.0f : TelemetryData.FuelLeft / TelemetryData.FuelPerLap;

    /// <summary>
    /// Fuel consumption per lap.
    /// </summary>
    public double FuelPerLap => TelemetryData.FuelPerLap <= 0 ? 0.0f : TelemetryData.FuelPerLap;

    /// <summary>
    /// Fuel required to reach session end based on session type.
    /// Assumes even fuel usage per lap or per time unit.
    /// </summary>
    public double FuelToEnd
    {
        get
        {
            switch ((Constant.SessionLengthFormat)TelemetryData.SessionLengthFormat)
            {
                case Constant.SessionLengthFormat.Unavailable:
                    return double.NaN; //Nothing to calculate
                // TimeBased
                case Constant.SessionLengthFormat.TimeBased: 
                    return TelemetryData.LapTimeBestSelf <= 0
                        ? 0.0
                        : (TelemetryData.SessionTimeRemaining / TelemetryData.LapTimeBestSelf) *
                          TelemetryData.FuelPerLap;
                
                // LapBased
                case Constant.SessionLengthFormat.LapBased:
                    if (TelemetryData.NumberOfLaps <= 0) return 0.0; // default safe value until telemetry is valid
                    return TelemetryData.NumberOfLaps * TelemetryData.FuelPerLap;
                
                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                case Constant.SessionLengthFormat.TimeAndLapBased:
                    if (TelemetryData.NumberOfLaps <= 0) return 0.0; // default safe value until telemetry is valid
                    return (TelemetryData.NumberOfLaps + 1) * TelemetryData.FuelPerLap;
                
                //No value / exception
                default:
                    return double.NaN;
            }
        }
    }

    /// <summary>
    /// Fuel needed to add to reach session end, constrained by tank capacity.
    /// </summary>
    public double FuelToAdd
    {
        get
        {
            double fuelToAdd;
            switch ((Constant.SessionLengthFormat)TelemetryData.SessionLengthFormat)
            {
                case Constant.SessionLengthFormat.Unavailable:
                    fuelToAdd = double.NaN; //Nothing to calculate
                    break;
                // TimeBased
                case Constant.SessionLengthFormat.TimeBased:
                // LapBased
                case Constant.SessionLengthFormat.LapBased:
                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                case Constant.SessionLengthFormat.TimeAndLapBased:
                    fuelToAdd = Math.Min(FuelToEnd - TelemetryData.FuelLeft,
                        TelemetryData.FuelCapacity - TelemetryData.FuelLeft);
                    break;
                default:
                    fuelToAdd = double.NaN;
                    break;
            }
            return fuelToAdd <= 0 ? 0.0 : fuelToAdd; 
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