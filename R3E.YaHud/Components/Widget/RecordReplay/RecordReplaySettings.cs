using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.RecordReplay
{
    /// <summary>
    /// Settings for the record and replay control widget.
    /// </summary>
    public class RecordReplaySettings : BasicSettings
    {
        private const string Red = "#FF3B30";
        private const string Green = "#008000";
        private const string Amber = "#FFB020";
        private const string Grey = "#B5B5B5";

        private WidgetDockSide dockSide = WidgetDockSide.Right;

        /// <summary>
        /// Where the widget lives. Docked to an edge it collapses to a slim rail that expands on
        /// hover, and - because a docked widget takes no drag handler - its transport controls work
        /// whether the HUD is locked or not. <see cref="WidgetDockSide.Free"/> keeps the original
        /// draggable behaviour for anyone who would rather place it themselves.
        /// </summary>
        [SettingType("Dock Side", SettingsTypes.Enum, 0,
            Description = "Dock to a screen edge as a hover-expanding rail, or leave free to position by hand")]
        public WidgetDockSide DockSide
        {
            get => dockSide;
            set
            {
                if (value == dockSide) return;
                dockSide = value;
                NotifyPropertyChanged();
            }
        }

        /// <inheritdoc />
        public override bool Docked => DockSide != WidgetDockSide.Free;

        [SettingType("Show Recording File", SettingsTypes.Checkbox, 1,
            Description = "Show the file the recorder is currently writing")]
        public bool ShowFileName { get; set; } = true;

        [SettingType("Show Dropped Frames", SettingsTypes.Checkbox, 2,
            Description = "Show how many frames the recorder had to drop")]
        public bool ShowDroppedFrames { get; set; } = true;

        [SettingType("Show Replay Readout", SettingsTypes.Checkbox, 3,
            Description = "Show the frame index, lap and session phase during replay")]
        public bool ShowReplayReadout { get; set; } = true;

        [SettingType("Recording Color", SettingsTypes.ColorPicker, 10,
            ViewMode = SettingsViewMode.Intermediate)]
        public string RecordingColor { get; set; } = Red;

        [SettingType("Replay Color", SettingsTypes.ColorPicker, 11,
            ViewMode = SettingsViewMode.Intermediate)]
        public string ReplayColor { get; set; } = Green;

        [SettingType("Catch Up Color", SettingsTypes.ColorPicker, 12,
            ViewMode = SettingsViewMode.Intermediate)]
        public string CatchUpColor { get; set; } = Amber;

        [SettingType("Label Color", SettingsTypes.ColorPicker, 13,
            ViewMode = SettingsViewMode.Intermediate)]
        public string LabelColor { get; set; } = Grey;
    }
}
