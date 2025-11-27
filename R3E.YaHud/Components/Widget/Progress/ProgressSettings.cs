using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.Progress
{
    public class ProgressSettings : BasicSettings
    {
        [SettingType("Positive Delta Time Color", SettingsTypes.ColorPicker, 10,
            Description = "Color of the delta time, when faster than previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string PositiveDeltaTimeColor { get; set; } = "#1BB12F";

        [SettingType("Negative Delta Time Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the delta time, when slower than previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NegativeDeltaTimeColor { get; set; } = "#B11B1B";

        [SettingType("Neutral Delta Time Color", SettingsTypes.ColorPicker, 12,
            Description = "Color of the delta time, when equal to previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NeutralDeltaTimeColor { get; set; } = "#FFFFFF";
    }
}
