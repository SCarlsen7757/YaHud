using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TvTower
{
    public class TvTowerSettings : BasicSettings
    {
        [SettingType("Text Color", SettingsTypes.ColorPicker, 10,
            ViewMode = SettingsViewMode.Intermediate)]
        public string TextColor { get; set; } = "#ffffff";

        [SettingType("Pit Stop Served Color", SettingsTypes.ColorPicker, 20,
            ViewMode = SettingsViewMode.Expert)]
        public string PitStopServedColor { get; set; } = "#007000";

        [SettingType("Pit Stop Not Served Color", SettingsTypes.ColorPicker, 21,
            ViewMode = SettingsViewMode.Expert)]
        public string PitStopNotServedColor { get; set; } = "#ad5c00";
    }
}
