using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;
using System.ComponentModel.DataAnnotations;

namespace R3E.YaHud.Components.Widget.RelativeDriver
{
    public enum GapDisplayMode
    {
        [Display(Name = "Time")]
        Time,
        [Display(Name = "Distance")]
        Distance
    }

    public class RelativeDriverSettings : BasicSettings
    {
        private GapDisplayMode gapMode = GapDisplayMode.Time;

        [SettingType("Gap Display Mode", SettingsTypes.Enum, 10,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Display gap as time or distance")]
        public GapDisplayMode GapMode
        {
            get => gapMode;
            set
            {
                if (value == gapMode) return;
                gapMode = value;
                NotifyPropertyChanged();
            }
        }

        [SettingType("Text Color", SettingsTypes.ColorPicker, 30,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Default text color")]
        public string TextColor { get; set; } = "#FFFFFFFF";

        [SettingType("Player Row Color", SettingsTypes.ColorPicker, 40,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Highlight color for player's row")]
        public string PlayerRowColor { get; set; } = "#FF4CAF";

        [SettingType("Lap Ahead Color", SettingsTypes.ColorPicker, 50,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for cars a lap ahead")]
        public string LapAheadColor { get; set; } = "#FF2196";

        [SettingType("Lap Behind Color", SettingsTypes.ColorPicker, 60,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for cars a lap behind")]
        public string LapBehindColor { get; set; } = "#FFFF98";

        [SettingType("Out Lap Color", SettingsTypes.ColorPicker, 70,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for cars on out lap (qualifying)")]
        public string OutLapColor { get; set; } = "#FFFFEB";

        [SettingType("Invalid Lap Color", SettingsTypes.ColorPicker, 80,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for cars on invalid lap (qualifying)")]
        public string InvalidLapColor { get; set; } = "#FFF443";

        [SettingType("Gap Positive Color", SettingsTypes.ColorPicker, 90,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for positive gap (behind)")]
        public string GapPositiveColor { get; set; } = "#FFFF52";

        [SettingType("Gap Negative Color", SettingsTypes.ColorPicker, 100,
            ViewMode = SettingsViewMode.Intermediate,
            Description = "Color for negative gap (ahead)")]
        public string GapNegativeColor { get; set; } = "#FF4CAF";
    }
}
