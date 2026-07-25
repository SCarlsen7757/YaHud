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
