# Code Review Instructions

When reviewing code, check for:

## Naming Conventions
- Private fields should use camelCase WITHOUT underscore prefix
- Use `this.` keyword when parameter names match field names
- PascalCase for public members, methods, and properties

## Common Issues to Flag
- `private int _fieldName` → Should be `private int fieldName`
- Missing `this.` when assigning parameters to same-named fields
- Inconsistent naming styles

## Widget Architecture

Widgets are **display-only components**. They should:

✅ **Allowed in Widgets:**
- Display data received from services
- Perform minor calculations for display purposes (e.g., formatting, unit conversion, rounding)
- Simple UI state management (e.g., visibility toggles, animations)

❌ **NOT Allowed in Widgets:**
- Logging data for future calculations
- Heavy calculations or data processing
- Storing historical data or state that persists across updates
- Business logic or complex computations

### When to Flag for Refactoring

If a Widget contains any of the following, flag it for refactoring:

1. **Data logging or history tracking** → Create a dedicated service class
2. **Heavy calculations** → Create a dedicated calculator/processor class
3. **Complex business logic** → Extract to a service class

### Correct Pattern

When heavy calculations or data logging is needed:

1. Create a new class responsible for the logic (e.g., `LapTimeCalculator.cs`, `FuelHistoryTracker.cs`)
2. Add an instance of that class to `TelemetryService.cs`
3. Widget should only read the processed results from the service

```csharp
// ❌ BAD: Widget doing heavy calculation and logging
public class FuelWidget : WidgetBase
{
    private List<double> fuelHistory = new();
    
    public void Update(TelemetryData data)
    {
        fuelHistory.Add(data.FuelLevel);
        var averageConsumption = CalculateAverageConsumption(fuelHistory);
        var lapsRemaining = PredictLapsRemaining(averageConsumption);
        // Display results
    }
}

// ✅ GOOD: Logic in dedicated class, Widget only displays
public class FuelCalculator
{
    private List<double> fuelHistory = new();
    
    public double AverageConsumption { get; private set; }
    public int PredictedLapsRemaining { get; private set; }
    
    public void Update(double fuelLevel)
    {
        fuelHistory.Add(fuelLevel);
        AverageConsumption = CalculateAverageConsumption();
        PredictedLapsRemaining = PredictLapsRemaining();
    }
}

// In TelemetryService.cs
public class TelemetryService
{
    private readonly FuelCalculator fuelCalculator;
    
    public FuelCalculator FuelCalculator => fuelCalculator;
    
    public TelemetryService()
    {
        fuelCalculator = new FuelCalculator();
    }
}

// Widget only reads and displays
public class FuelWidget : WidgetBase
{
    public void Update(TelemetryService telemetry)
    {
        DisplayConsumption(telemetry.FuelCalculator.AverageConsumption);
        DisplayLapsRemaining(telemetry.FuelCalculator.PredictedLapsRemaining);
    }
}
```

### Summary

| Responsibility | Location |
|----------------|----------|
| Display data | Widget |
| Minor display formatting | Widget |
| Data logging/history | Dedicated class → `TelemetryService.cs` |
| Heavy calculations | Dedicated class → `TelemetryService.cs` |
| Business logic | Dedicated class → `TelemetryService.cs` |