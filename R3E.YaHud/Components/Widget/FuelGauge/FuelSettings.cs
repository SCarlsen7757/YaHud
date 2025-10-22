using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.FuelGauge
{
    public class FuelSettings : BasicSettings
    {
        [SettingType("Fuel Green", SettingsTypes.ColorPicker, 10,
            Description = "Color of the RPM bar",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmColor { get; set; } = "#0069ff";
        

        [SettingType("RPM Redline Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the RPM bar when in the redline zone",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmUpshiftColor { get; set; } = "#ff006a";
    }
}
