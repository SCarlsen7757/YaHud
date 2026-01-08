using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.MoTec
{
    public class MoTecSettings : BasicSettings
    {
        [SettingType("Primary Color", SettingsTypes.ColorPicker, 10,
            Description = "Color of the RPM bar and gear indicator",
            ViewMode = SettingsViewMode.Intermediate)]
        public string PrimaryColor { get; set; } = "#0069ff";

        [SettingType("RPM Redline Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the RPM bar when in the redline zone",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmUpshiftColor { get; set; } = "#ff006a";

        [SettingType("RPM Max Color", SettingsTypes.ColorPicker, 12,
            Description = "Color of the RPM bar when at maximum RPM",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmMaxColor { get; set; } = "#ff0000";

        [SettingType("RPM Blink Color", SettingsTypes.ColorPicker, 13,
            Description = "Color of the RPM bar when blinking",
            ViewMode = SettingsViewMode.Intermediate)]
        public string SecondaryBlinkColor { get; set; } = "#5e5e5e";
    }
}
