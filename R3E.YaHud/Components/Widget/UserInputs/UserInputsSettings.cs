using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.UserInputs
{
    public class UserInputsSettings : BasicSettings
    {
        [SettingType("Show Steering input", SettingsTypes.Checkbox, 10,
            Description = "Show steering input",
            ViewMode = SettingsViewMode.Beginner)]
        public bool ShowSteering { get; set; } = true;

        [SettingType("Show Clutch", SettingsTypes.Checkbox, 15,
            Description = "Show clutch input",
            ViewMode = SettingsViewMode.Intermediate)]
        public bool ShowClutch { get; set; } = true;

        [SettingType("Clutch Color", SettingsTypes.ColorPicker, 20,
            Description = "Color of the RPM bar",
            ViewMode = SettingsViewMode.Expert)]
        public string ClutchColor { get; set; } = "#ffdc1c";

        [SettingType("Throttle Color", SettingsTypes.ColorPicker, 21,
            Description = "Color of the RPM bar",
            ViewMode = SettingsViewMode.Expert)]
        public string ThrottleColor { get; set; } = "#1ea5ff";

        [SettingType("Brake Color", SettingsTypes.ColorPicker, 22,
            Description = "Color of the RPM bar",
            ViewMode = SettingsViewMode.Expert)]
        public string BrakeColor { get; set; } = "#ff1e1e";
    }
}
