using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TireBrakeInfo
{
    public class TireBrakeInfoSettings : BasicSettings
    {
        [SettingType("Cold tire color", SettingsTypes.ColorPicker, 1,
            Description = "Color used when the tire is in the cold temperature range", 
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireColdColor { get; set; } = "#a9a9ff";
        [SettingType("Optimal tire color", SettingsTypes.ColorPicker, 2,
            Description = "Color used when the tire is the optimal temperature range",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireOptimalColor { get; set; } = "#00c300";
        [SettingType("Hot tire color", SettingsTypes.ColorPicker, 3,
            Description = "Color used when the tire is in the hot temperature range",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireHotColor { get; set; } = "#ff0000";
        [SettingType("Cold brake color", SettingsTypes.ColorPicker, 4,
            Description = "Color used when the brake is in the cold temperature range",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeColdColor { get; set; } = "#a9a9ff";
        [SettingType("Optimal brake color", SettingsTypes.ColorPicker, 5,
            Description = "Color used when the brake is in the optimal temperature range",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeOptimalColor { get; set; } = "#00c300";
        [SettingType("Hot brake color", SettingsTypes.ColorPicker, 6,
            Description = "Color used when the brake is in the hot temperature range",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeHotColor { get; set; } = "#ff0000";

        [SettingType("Tire choice background color", SettingsTypes.ColorPicker, 7,
            Description = "Color used for the background of the tire choice",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireChoiceBackgroundColor { get; set; } = "#2b2727";
        [SettingType("Tire age background color", SettingsTypes.ColorPicker, 8,
            Description = "Color used for the background of the tire age",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireAgeBackgroundColor { get; set; } = "#a18e8e";

    }
}
