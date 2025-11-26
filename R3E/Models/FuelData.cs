using R3E.API;
using R3E.Data;

namespace R3E.Models;

public class FuelData: IDisposable
{
    private volatile bool disposed = false;
    private readonly Shared telemetryData;
    private readonly ITelemetryService telemetryService;
    private double oldFuelRemaining = 0.0f;
    public double LastLapFuelUsage { get; set; } = 0.0f;
    
    public FuelData(Shared telemetryData, ITelemetryService telemetryService)
    {
        this.telemetryData = telemetryData;
        this.telemetryService = telemetryService;
        telemetryService.NewLap += TelemetryServiceOnNewLap;
        telemetryService.SessionPhaseChanged += TelemetryServiceOnSessionPhaseChanged;
    }

    private void TelemetryServiceOnSessionPhaseChanged(TelemetryData obj)
    {
        if ((Constant.SessionPhase)telemetryData.SessionPhase == Constant.SessionPhase.Countdown)
        {
            oldFuelRemaining = telemetryData.FuelLeft;
        }
    }

    private void TelemetryServiceOnNewLap(TelemetryData obj)
    {
        //Everytime we pass raceline NumberOfLaps should update so we use this as a trigger to calculate the remaining variables 
        LastLapFuelUsage = oldFuelRemaining - telemetryData.FuelLeft;
        oldFuelRemaining = telemetryData.FuelLeft;
    }

    public double FuelLeft
    {
        get
        {
            if (telemetryData.FuelLeft <= 0) return 0.0f;
            return telemetryData.FuelLeft;
        }
    }
    
    public double TimeEstimatedLeft
        {
            get
            {
                if (telemetryData.LapTimeBestSelf <= 0 || telemetryData.FuelPerLap <= 0) return double.NaN;
                return (telemetryData.FuelLeft / telemetryData.FuelPerLap) * telemetryData.LapTimeBestSelf;
            }
        }
    public double FuelRemainingProcentage
    {
        get
        {
            if (telemetryData.FuelCapacity <= 0) return double.NaN;
            return (telemetryData.FuelLeft / telemetryData.FuelCapacity) * 100;
        }
    }

    public double LapsEstimatedLeft
    {
        get
        {
            if (telemetryData.FuelPerLap <= 0) return double.NaN;
            return telemetryData.FuelLeft / telemetryData.FuelPerLap;
        }
    }
        
    public double FuelPerLap
    {
        get
        {
            if (telemetryData.FuelPerLap <= 0) return double.NaN;
            return telemetryData.FuelPerLap;
        }
    }

    public double FuelToEnd
    {
        get 
        {
            switch ((Constant.SessionLengthFormat)telemetryData.SessionLengthFormat) 
            {
                case Constant.SessionLengthFormat.Unavailable:
                    return double.NaN; //Nothing to calculate
                
                // TimeBased
                case Constant.SessionLengthFormat.TimeBased: 
                    return (telemetryData.SessionTimeRemaining / telemetryData.LapTimeBestSelf) * telemetryData.FuelPerLap;

                // LapBased
                case Constant.SessionLengthFormat.LapBased:
                    return telemetryData.NumberOfLaps / telemetryData.FuelPerLap;
                
                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                case Constant.SessionLengthFormat.TimeAndLapBased:
                    return (telemetryData.NumberOfLaps+1) / telemetryData.FuelPerLap;
            
                 default: 
                    return double.NaN;
            } 
        }
    }

    public double FuelToAdd
    {
        get 
        {
            switch ((Constant.SessionLengthFormat)telemetryData.SessionLengthFormat) 
            {
                case Constant.SessionLengthFormat.Unavailable:
                    return double.NaN; //Nothing to calculate

                // TimeBased
                case Constant.SessionLengthFormat.TimeBased: 
                    return Math.Min(FuelToEnd - telemetryData.FuelLeft, telemetryData.FuelCapacity - telemetryData.FuelLeft); 

                // LapBased
                case Constant.SessionLengthFormat.LapBased:
                    return Math.Min(FuelToEnd - telemetryData.FuelLeft, telemetryData.FuelCapacity - telemetryData.FuelLeft); 

                // TimeAndLapBased - Time and lap based session means there will be an extra lap after the time has run out
                case Constant.SessionLengthFormat.TimeAndLapBased:
                    return Math.Min(FuelToEnd - telemetryData.FuelLeft, telemetryData.FuelCapacity - telemetryData.FuelLeft); 
            
                default: 
                    return double.NaN;
            } 
        } 
    }

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