using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TvTower
{
    public class TvTowerSettings : BasicSettings
    {
        [SettingType("Text Color", SettingsTypes.ColorPicker, 10,
            ViewMode = SettingsViewMode.Intermediate)]
        public string TextColor { get; set; } = "#FFFFFFFF";
    }
}
