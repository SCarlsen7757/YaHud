using R3E.API;

namespace R3E.YaHud.Services.Settings
{
    public class GlobalSettings
    {
        public SettingsViewMode ViewMode { get; set; } = SettingsViewMode.Beginner;
        public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.KilometersPerHour;
        public AngularUnit AngularUnit { get; set; } = AngularUnit.RevolutionsPerMinute;
        public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    }
}
