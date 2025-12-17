
TODO: 🔧 Fuel Widget Refactoring Task List
Architecture & Separation of Concerns

- [x] Move fuel calculation logic from widget to TelemetryData class
- [x] Add LastLapFuelUsage property with internal calculation logic
- [x] Add FuelToEnd property with session-aware calculation (time/lap/hybrid)
- [x] Add FuelToAdd property with capacity constraints
- [x] Add TimeEstimatedLeft property (TimeSpan)
- [x] Add RemainingLaps property (calculated from fuel)
- [x] Track lap-to-lap fuel consumption internally (using NewLap event pattern)
- [x] Handle session phase state tracking (countdown initialization)
- [x] Store oldFuelRemaining and session-specific data inside TelemetryData
- [x] Create a dedicated FuelCalculationService (alternative approach)
- [x] Implement service to handle heavy fuel calculations
- [x] Subscribe to TelemetryService.NewLap event
- [x] Expose calculated properties via events or properties
- [x] Handle session phase transitions and one-shot initialization

Widget Simplification
-	[x] Reduce widget to display-only logic
-	[x] Remove all calculation logic from Update() method
-	[x] Remove state tracking variables (oldNumberOfLaps, oldFuelRemaining, etc.)
-	[x] Remove OnNewLap event handler from widget
-	[x] Access pre-calculated values from TelemetryData or service
-	[x] Keep only display formatting logic in widget
-	[x] Clean up variable naming and types
-	[x] Rename sTimeEstimatedLeft → TimeEstimatedLeftFormatted (follow C# conventions)
-	[x] Use double instead of float for consistency with other widgets
-	[x] Remove redundant FRColorSwitch and FTEColorSwitch intermediate variables
-	[x] Remove unused NumberOfLaps property (use data.NumberOfLaps directly)

Code Quality & Consistency
-	[ ] Fix HTML/CSS issues
-	[x] Fix missing space in style="color: @FRColor" (all instances)
-	[x] Replace hardcoded RGB colors with Settings properties or constants
-	[x] Consider creating color helper methods similar to other widgets
-	[x] Improve color logic
-	[x] Extract color threshold logic to helper methods
-	[x] Use consistent color naming (follow MoTec pattern with RpsCurrentColor)
-	[x] Move hardcoded colors to FuelSettings class
-	[x] Consider using percentage-based thresholds from settings
-	[x] Remove unused imports
-	[x] Remove System.Drawing
-	[x] Remove System.Runtime.InteropServices
-	[x] Remove System.Diagnostics.Eventing.Reader
-	[x] Fix formatting issues
-	[x] Add consistent spacing around => in property accessors
-	[x] Use consistent indentation for switch statements
-	[x] Remove empty statement in Unavailable case (replace ; with break;)
Data Access Pattern
-	[x] Follow established widget patterns
-	[x] Access telemetry data through TelemetryService.Data.Raw (already correct)
-	[x] Use computed properties from TelemetryData instead of raw calculations
-	[x] Implement display formatting only in widget
-	[x] Use SettingsService.GlobalSettings for unit conversions if needed
Settings Integration
-	[x] Review and expand FuelSettings
-	[x] Add color threshold settings (currently using hardcoded values)
-	[x] Add display unit preferences (ONLY L, NOT APPLICABLE)
-	[x] Add color properties for all hardcoded RGB values
-	[x] Ensure all colors follow the pattern: green, yellow, orange, red
Time Formatting
-	[x] Refactor time display logic
-	[x] Extract time formatting to helper method
-	[x] Consider moving to utility class for reuse
-	[x] Simplify color selection logic based on time thresholds
Dispose Pattern
-	[x] Fix disposal implementation
-	[x] Call base.Dispose() instead of just unsubscribing event
-	[x] Follow disposal pattern from HudWidgetBase
Testing & Validation
-	[x Improve test data
-	[x] Ensure UpdateWithTestData() represents realistic scenarios
-	[x] Test all color states
-	[x] Test all session length formats
Documentation
-	[x] Add XML documentation
-	[x] Document properties (purpose and units)
-	[x] Document calculation assumptions
-	[x] Add comments explaining session phase logic
---
🎯 Priority Order
-1.	High Priority: Move calculations to TelemetryData or create FuelCalculationService
0.	High Priority: Simplify widget to display-only logic
1.	Medium Priority: Fix code quality issues (naming, formatting, unused imports)
2.	Medium Priority: Extract hardcoded colors to settings
3.	Low Priority: Documentation and test improvements
      This refactoring will align the Fuel widget with the architecture used by UserInputs, MoTec, and other widgets in the solution, where widgets are lightweight display components and heavy logic resides in services or data classes.