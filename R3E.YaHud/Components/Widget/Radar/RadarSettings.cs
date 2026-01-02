using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;


namespace R3E.YaHud.Components.Widget.Radar
{
    public class RadarSettings : BasicSettings
    {
        [SettingType("Radar rage", SettingsTypes.Number, 1, Description = "Range of the radar to start detecting opponents in meters", ViewMode =SettingsViewMode.Expert, Max = 80, Min = 1)]
        public int RadarRange { get; set; } = 12;
        public float RadarOpacity { get; set; } = 0.8f;
        public int RadarFadeRange { get; set; } = 2;

        [SettingType("Auto hide radar", SettingsTypes.Checkbox, 0,Description ="Hides the radar if no opponents are within radar range", ViewMode = SettingsViewMode.Beginner)]
        public bool AutoHideRadar { get; set; } = true;

        public bool Use3DTranslate { get; set; } = true;
    }
}
