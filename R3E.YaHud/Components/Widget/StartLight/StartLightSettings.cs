using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.StartLight
{

    public class StartLightSettings : BasicSettings
    {
        [SettingType("Start color", SettingsTypes.ColorPicker, 20,
            Description = "Color of the 'Start' light.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string StartColor { get; set; } = "#00FF00";

        [SettingType("Ready color", SettingsTypes.ColorPicker, 21,
            Description = "Color of the 'Ready' light.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string ReadyColor { get; set; } = "#FF0000";

        [SettingType("Formation lap color", SettingsTypes.ColorPicker, 22,
            Description = "Color of the 'Formation Lap' light.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string FormationLapColor { get; set; } = "#0000FF";

        [SettingType("Widget visible after start duration (ms)", SettingsTypes.Slider, 30,
            Description = "Duration the green light stays on before turning off.",
            Max = 5000,
            Min = 0,
            Step = 100,
            ViewMode = SettingsViewMode.Intermediate)]
        public int VisibleAfterStartDurationMs { get; set; } = 2500;

    }
}
