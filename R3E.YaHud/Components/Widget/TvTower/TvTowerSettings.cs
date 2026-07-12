using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TvTower
{
    public class TvTowerSettings : BasicSettings
    {
        private const string ColorsCategory = "Colors";
        private const string PitStopCategory = "Pit Stop";
        private const string DriverRowsCategory = "Driver Rows";

        private bool showPitWindow = true;
        private bool colorHeaderBySessionFlag = false;
        private int topDriverCount = 3;
        private int contextDriverCount = 2;

        [SettingType("Text Color", SettingsTypes.ColorPicker, 1,
            ViewMode = SettingsViewMode.Intermediate,
            Category = ColorsCategory)]
        public string TextColor { get; set; } = "#ffffff";

        [SettingType("Header Color", SettingsTypes.ColorPicker, 2,
            Description = "Table header background color",
            ViewMode = SettingsViewMode.Intermediate,
            Category = ColorsCategory)]
        public string HeaderColor { get; set; } = "#1E1E1E";

        [SettingType("Player Row Color", SettingsTypes.ColorPicker, 3,
            Description = "Highlight color for player's row",
            ViewMode = SettingsViewMode.Intermediate,
            Category = ColorsCategory)]
        public string PlayerRowColor { get; set; } = "#FF4CAF";

        [SettingType("Color Header By Session Flag", SettingsTypes.Checkbox, 4,
            Description = "Color the table header by the current session flag (yellow, green, checkered)",
            ViewMode = SettingsViewMode.Intermediate,
            Category = ColorsCategory)]
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



        [SettingType("Show Pit Window", SettingsTypes.Checkbox, 1,
            Description = "Show mandatory pit window status above the table",
            ViewMode = SettingsViewMode.Beginner,
            Category = PitStopCategory)]
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

        [SettingType("Pit Stop Served Color", SettingsTypes.ColorPicker, 2,
            ViewMode = SettingsViewMode.Expert,
            Category = PitStopCategory)]
        public string PitStopServedColor { get; set; } = "#007000";

        [SettingType("Pit Stop Not Served Color", SettingsTypes.ColorPicker, 3,
            ViewMode = SettingsViewMode.Expert,
            Category = PitStopCategory)]
        public string PitStopNotServedColor { get; set; } = "#ad5c00";



        [SettingType("Top Driver Count", SettingsTypes.Number, 1,
            Min = 1, Max = 10, Step = 1,
            Description = "Number of top drivers always shown before the split",
            ViewMode = SettingsViewMode.Expert,
            Category = DriverRowsCategory)]
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

        [SettingType("Context Driver Count", SettingsTypes.Number, 2,
            Min = 1, Max = 5, Step = 1,
            Description = "Number of drivers shown ahead and behind the player when the table is split",
            ViewMode = SettingsViewMode.Expert,
            Category = DriverRowsCategory)]
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
