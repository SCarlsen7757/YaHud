using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.TireBrakeInfo
{
    public class TireBrakeInfoSettings : BasicSettings
    {
        private string tireColdColor = "#a9a9ff";
        [SettingType("Cold tire color", SettingsTypes.ColorPicker, 1,
            Description = "Color used when the tire is in the cold temperature range",
            Category = "Tire Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireColdColor
        {
            get => tireColdColor;
            set
            {
                if (value == tireColdColor) return;
                tireColdColor = value;
                NotifyPropertyChanged();
            }
        }

        private string tireOptimalColor = "#00c300";
        [SettingType("Optimal tire color", SettingsTypes.ColorPicker, 2,
            Description = "Color used when the tire is the optimal temperature range",
            Category = "Tire Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireOptimalColor
        {
            get => tireOptimalColor;
            set
            {
                if (value == tireOptimalColor) return;
                tireOptimalColor = value;
                NotifyPropertyChanged();
            }
        }

        private string tireHotColor = "#ff0000";
        [SettingType("Hot tire color", SettingsTypes.ColorPicker, 3,
            Description = "Color used when the tire is in the hot temperature range",
            Category = "Tire Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireHotColor
        {
            get => tireHotColor;
            set
            {
                if (value == tireHotColor) return;
                tireHotColor = value;
                NotifyPropertyChanged();
            }
        }

        private string brakeColdColor = "#a9a9ff";
        [SettingType("Cold brake color", SettingsTypes.ColorPicker, 4,
            Description = "Color used when the brake is in the cold temperature range",
            Category = "Brake Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeColdColor
        {
            get => brakeColdColor;
            set
            {
                if (value == brakeColdColor) return;
                brakeColdColor = value;
                NotifyPropertyChanged();
            }
        }

        private string brakeOptimalColor = "#00c300";
        [SettingType("Optimal brake color", SettingsTypes.ColorPicker, 5,
            Description = "Color used when the brake is in the optimal temperature range",
            Category = "Brake Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeOptimalColor
        {
            get => brakeOptimalColor;
            set
            {
                if (value == brakeOptimalColor) return;
                brakeOptimalColor = value;
                NotifyPropertyChanged();
            }
        }

        private string brakeHotColor = "#ff0000";
        [SettingType("Hot brake color", SettingsTypes.ColorPicker, 6,
            Description = "Color used when the brake is in the hot temperature range",
            Category = "Brake Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string BrakeHotColor
        {
            get => brakeHotColor;
            set
            {
                if (value == brakeHotColor) return;
                brakeHotColor = value;
                NotifyPropertyChanged();
            }
        }

        [SettingType("Tire choice background color", SettingsTypes.ColorPicker, 7,
            Description = "Color used for the background of the tire choice",
            Category = "Tire Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireChoiceBackgroundColor { get; set; } = "#2b2727";
        [SettingType("Tire age background color", SettingsTypes.ColorPicker, 8,
            Description = "Color used for the background of the tire age",
            Category = "Tire Color",
            ViewMode = SettingsViewMode.Intermediate)]
        public string TireAgeBackgroundColor { get; set; } = "#a18e8e";

    }
}
