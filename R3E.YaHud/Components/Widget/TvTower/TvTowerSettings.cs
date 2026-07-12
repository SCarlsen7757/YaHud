using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TvTower
{
    public class TvTowerSettings : BasicSettings
    {
        private bool showPitWindow = true;
        private bool colorHeaderBySessionFlag = false;
        private int topDriverCount = 3;
        private int contextDriverCount = 2;

        [SettingType("Text Color", SettingsTypes.ColorPicker, 10,
            ViewMode = SettingsViewMode.Intermediate)]
        public string TextColor { get; set; } = "#ffffff";

        [SettingType("Pit Stop Served Color", SettingsTypes.ColorPicker, 20,
            ViewMode = SettingsViewMode.Expert)]
        public string PitStopServedColor { get; set; } = "#007000";

        [SettingType("Pit Stop Not Served Color", SettingsTypes.ColorPicker, 21,
            ViewMode = SettingsViewMode.Expert)]
        public string PitStopNotServedColor { get; set; } = "#ad5c00";

        [SettingType("Header Color", SettingsTypes.ColorPicker, 30,
            Description = "Table header background color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string HeaderColor { get; set; } = "#1E1E1E";

        [SettingType("Player Row Color", SettingsTypes.ColorPicker, 40,
            Description = "Highlight color for player's row",
            ViewMode = SettingsViewMode.Intermediate)]
        public string PlayerRowColor { get; set; } = "#FF4CAF";

        [SettingType("Show Pit Window", SettingsTypes.Checkbox, 50,
            Description = "Show mandatory pit window status above the table",
            ViewMode = SettingsViewMode.Beginner)]
        public bool ShowPitWindow
        {
            get => showPitWindow;
            set
            {
                if (value == showPitWindow) return;
                showPitWindow = value;
                NotifyPropertyChanged();
            }
        }

        [SettingType("Color Header By Session Flag", SettingsTypes.Checkbox, 60,
            Description = "Color the table header by the current session flag (yellow, green, checkered)",
            ViewMode = SettingsViewMode.Intermediate)]
        public bool ColorHeaderBySessionFlag
        {
            get => colorHeaderBySessionFlag;
            set
            {
                if (value == colorHeaderBySessionFlag) return;
                colorHeaderBySessionFlag = value;
                NotifyPropertyChanged();
            }
        }

        [SettingType("Top Driver Count", SettingsTypes.Number, 70,
            Min = 1, Max = 10, Step = 1,
            Description = "Number of top drivers always shown before the split",
            ViewMode = SettingsViewMode.Expert)]
        public int TopDriverCount
        {
            get => topDriverCount;
            set
            {
                if (value == topDriverCount) return;
                topDriverCount = value;
                NotifyPropertyChanged();
            }
        }

        [SettingType("Context Driver Count", SettingsTypes.Number, 71,
            Min = 1, Max = 5, Step = 1,
            Description = "Number of drivers shown ahead and behind the player when the table is split",
            ViewMode = SettingsViewMode.Expert)]
        public int ContextDriverCount
        {
            get => contextDriverCount;
            set
            {
                if (value == contextDriverCount) return;
                contextDriverCount = value;
                NotifyPropertyChanged();
            }
        }
    }
}
