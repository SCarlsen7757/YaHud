using R3E.YaHud.Services.Settings;
using System.Reflection;

namespace R3E.YaHud.Components.UI.Components.Settings
{
    public class SettingInfo
    {
        public PropertyInfo Prop { get; set; } = null!;
        public SettingTypeAttribute? Attr { get; set; }
    }
}
