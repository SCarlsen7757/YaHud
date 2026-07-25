using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Components.UI.Components.Settings
{
    public sealed class SettingsContext
    {
        public Action<IWidget>? OpenDetails { get; set; }
        public IWidget? SelectedWidget { get; set; }

        /// <summary>Opens the recordings browser in the detail area.</summary>
        public Action? OpenRecordings { get; set; }

        /// <summary>Whether the detail area is showing the recordings browser rather than a widget.</summary>
        public bool RecordingsSelected { get; set; }
    }
}
