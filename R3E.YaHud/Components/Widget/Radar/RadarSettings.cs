using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;


namespace R3E.YaHud.Components.Widget.Radar
{
    public class RadarSettings : BasicSettings
    {
        [SettingType("Radar rage", SettingsTypes.Number, 1, Description = "Range of the radar to start detecting opponents in meters", ViewMode = SettingsViewMode.Expert, Max = 80, Min = 1)]
        public int RadarRange { get; set; } = 12;
        public float RadarOpacity { get; set; } = 0.8f;
        public int RadarFadeRange { get; set; } = 2;

        [SettingType("Auto hide radar", SettingsTypes.Checkbox, 0, Description = "Hides the radar if no opponents are within radar range", ViewMode = SettingsViewMode.Beginner)]
        public bool AutoHideRadar { get; set; } = true;
        [SettingType("Radar beeping", SettingsTypes.Checkbox, 0, Description = "Use Translate3D for radar car placement (Performance option)", ViewMode = SettingsViewMode.Expert)]
        public bool Use3DTranslate { get; set; } = true;

        [SettingType("Minimum beeping speed", SettingsTypes.Number, 1, Description = "Minimum speed for the radar beeping to begin in KPH", ViewMode = SettingsViewMode.Expert, Max = 80, Min = 15)]
        public int MinBeepSpeed { get; set; } = 15;

        [SettingType("Radar beeping", SettingsTypes.Checkbox, 0, Description = "Have the radar beeping at you when an opponent is on either side of you", ViewMode = SettingsViewMode.Beginner)]
        public bool RadarBeeping { get; set; } = false;
        [SettingType("Beeping speed", SettingsTypes.Number, 1, Description = "Interval of of fast the beeping runs", ViewMode = SettingsViewMode.Expert, Max = 5000, Min = 50)]
        public int BeepingInterval { get; set; } = 200;
    }
}
