using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.FuelGauge
{
    public class FuelGaugeSettings : BasicSettings
    {
        [SettingType("Normal Color", SettingsTypes.ColorPicker, 10,
            Description = "Color of value bar and icon.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NormalColor { get; set; } = "#ffffff";

        [SettingType("Low Color", SettingsTypes.ColorPicker, 10,
            Description = "Color of value bar and icon when fuel level is low.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string LowColor { get; set; } = "#ff0000";
    }
}
