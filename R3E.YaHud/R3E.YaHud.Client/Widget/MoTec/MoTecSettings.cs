using R3E.YaHud.Client.Services.Settings;
using R3E.YaHud.Client.Widget.Core;

namespace R3E.YaHud.Client.Widget.MoTec
{
    public class MoTecSettings : BasicSettings
    {
        [SettingType("RPM Color", SettingsTypes.ColorPicker,
            Description = "Color of the RPM bar",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmColor { get; set; } = "#0069ff";

        [SettingType("RPM Redline Color", SettingsTypes.ColorPicker,
            Description = "Color of the RPM bar when in the redline zone",
            ViewMode = SettingsViewMode.Intermediate)]
        public string RpmUpshiftColor { get; set; } = "#ff006a";
    }
}
