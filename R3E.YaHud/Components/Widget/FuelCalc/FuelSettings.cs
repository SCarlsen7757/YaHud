using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.FuelCalc
{
    public class FuelSettings : BasicSettings
    {
        private const string Red  = "#FF0000";
        private const string Yellow  = "#FFFF00";
        private const string Orange = "#FF6600";
        private const string Green  = "#008000";
        private const string DimmedYellow = "#7F7F2B";
        
        [SettingType("FuelPerLapColor", SettingsTypes.ColorPicker, 10,
            ViewMode = SettingsViewMode.Intermediate)]
        public string FuelPerLapColor { get; set; } = DimmedYellow;
        
        [SettingType("LapsEstimatedLeftColor", SettingsTypes.ColorPicker, 11,
            ViewMode = SettingsViewMode.Intermediate)]
        public string LapsEstimatedLeftColor { get; set; } = Green;
        
        [SettingType("FuelToAddColor", SettingsTypes.ColorPicker, 12,
            ViewMode = SettingsViewMode.Intermediate)]
        public string FuelToAddColor { get; set; } = Green;
        
        [SettingType("LastLapFuelUsageColor", SettingsTypes.ColorPicker, 13,
            ViewMode = SettingsViewMode.Intermediate)]
        public string LastLapFuelUsageColor { get; set; } = DimmedYellow;
        
        [SettingType("ColorHigh", SettingsTypes.ColorPicker, 14,
            ViewMode = SettingsViewMode.Intermediate)]
        public string ColorHigh { get; set; } = Green; 
        
        [SettingType("ColorMidHigh", SettingsTypes.ColorPicker, 15,
            ViewMode = SettingsViewMode.Intermediate)]
        public string ColorMidHigh { get; set; } = Yellow;
        
        [SettingType("ColorMidLow", SettingsTypes.ColorPicker, 16,
            ViewMode = SettingsViewMode.Intermediate)]
        public string ColorMidLow { get; set; } = Orange;
        
        [SettingType("ColorLow", SettingsTypes.ColorPicker, 17,
            ViewMode = SettingsViewMode.Intermediate)]
        public string ColorLow { get; set; } = Red;
    }
}
