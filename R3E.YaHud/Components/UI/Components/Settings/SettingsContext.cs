using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Components.UI.Components.Settings
{
    public sealed class SettingsContext
    {
        public Action<IWidget>? OpenDetails { get; set; }
        public IWidget? SelectedWidget { get; set; }
    }
}
