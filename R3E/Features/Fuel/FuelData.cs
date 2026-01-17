using R3E.Core.Services;
using R3E.Data;

namespace R3E.Features.Fuel;

/// <summary>
/// Pure data class containing fuel-related calculations.
/// Mutable data holder updated by FuelService - no event subscriptions or service logic.
/// </summary>
public class FuelData
{
    private readonly TelemetryData telemetryData;
    private Shared Raw => telemetryData.Raw;

    internal FuelData(TelemetryData telemetryData)
    {
        this.telemetryData = telemetryData;
    }

    /// <summary>
    /// Total fuel capacity of the vehicle (liters).
    /// </summary>
    public double FuelCapacity => Raw.FuelCapacity;

    /// <summary>
    /// Fuel used in the last completed lap (liters).
    /// Updated by FuelService when a lap completes.
    /// </summary>
    public double LastLapFuelUsage { get; internal set; }

    /// <summary>
    /// Current fuel left in the tank (liters). Returns 0 if negative.
    /// </summary>
    public double FuelLeft => Raw.FuelLeft <= 0.0 ? 0.0 : Raw.FuelLeft;

    /// <summary>
    /// Estimated time left in the session based on fuel and lap time (seconds).
    /// Returns 0 if lap time or fuel per lap is unavailable.
    /// </summary>
    public double TimeEstimatedLeft => Raw.LapTimeBestSelf <= 0.0 || Raw.FuelPerLap <= 0.0
        ? 0.0
        : (Raw.FuelLeft / Raw.FuelPerLap) * Raw.LapTimeBestSelf;

    /// <summary>
    /// Fuel remaining as a percentage of total capacity.
    /// Returns 0 if capacity is zero.
    /// </summary>
    public double FuelRemainingPercentage => Raw.FuelCapacity <= 0.0
        ? 0.0
        : (Raw.FuelLeft / Raw.FuelCapacity) * 100.0;

    /// <summary>
    /// Estimated laps remaining with current fuel.
    /// </summary>
    public double LapsEstimatedLeft => Raw.FuelPerLap <= 0.0
        ? 0.0
        : Raw.FuelLeft / Raw.FuelPerLap;

    /// <summary>
    /// Fuel consumption per lap (liters).
    /// </summary>
    public double FuelPerLap => Raw.FuelPerLap <= 0.0 ? 0.0 : Raw.FuelPerLap;

    /// <summary>
    /// Fuel required to reach session end based on session type.
    /// Assumes even fuel usage per lap or per time unit.
    /// </summary>
    public double FuelToEnd
    {
        get
        {
            return (Constant.SessionLengthFormat)Raw.SessionLengthFormat switch
            {
                Constant.SessionLengthFormat.Unavailable => double.NaN,

                Constant.SessionLengthFormat.TimeBased => Raw.LapTimeBestSelf <= 0.0
                    ? 0.0
                    : (Raw.SessionTimeRemaining / Raw.LapTimeBestSelf) * Raw.FuelPerLap,

                Constant.SessionLengthFormat.LapBased => Raw.NumberOfLaps <= 0
                    ? 0.0
                    : Raw.NumberOfLaps * Raw.FuelPerLap,

                Constant.SessionLengthFormat.TimeAndLapBased => Raw.NumberOfLaps <= 0
                    ? 0.0
                    : (Raw.NumberOfLaps + 1) * Raw.FuelPerLap,

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
            var fuelNeeded = (Constant.SessionLengthFormat)Raw.SessionLengthFormat switch
            {
                Constant.SessionLengthFormat.Unavailable => double.NaN,
                Constant.SessionLengthFormat.TimeBased or
                Constant.SessionLengthFormat.LapBased or
                Constant.SessionLengthFormat.TimeAndLapBased =>
                    Math.Min(FuelToEnd - Raw.FuelLeft, Raw.FuelCapacity - Raw.FuelLeft),
                _ => double.NaN
            };

            return fuelNeeded <= 0.0 ? 0.0 : fuelNeeded;
        }
    }
}