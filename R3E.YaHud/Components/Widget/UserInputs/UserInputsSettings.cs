using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.UserInputs
{
    public class UserInputsSettings : BasicSettings
    {
        private bool showSteeringWheel = true;

        [SettingType("Show Steering wheel", SettingsTypes.Checkbox, 10,
            Description = "Show steering wheel",
            ViewMode = SettingsViewMode.Beginner)]
        public bool ShowSteeringWheel
        {
            get => showSteeringWheel;
            set
            {
                if (value == showSteeringWheel) return;
                showSteeringWheel = value;
                NotifyPropertyChanged();
            }
        }

        private string steeringColor = "#ffffff";

        [SettingType("Steering wheel Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the steering wheel",
            ViewMode = SettingsViewMode.Expert)]
        public string SteeringColor
        {
            get => steeringColor;
            set
            {
                if (value == steeringColor) return;
                steeringColor = value;
                NotifyPropertyChanged();
            }
        }

        private bool showSteeringInput = true;

        [SettingType("Show Steering Input", SettingsTypes.Checkbox, 12,
            Description = "Show steering input bar",
            ViewMode = SettingsViewMode.Beginner)]
        public bool ShowSteeringInput
        {
            get => showSteeringInput;
            set
            {
                if (value == showSteeringInput) return;
                showSteeringInput = value;
                NotifyPropertyChanged();
            }
        }

        private string steeringInputColor = "#ffffff";

        [SettingType("Steering Input Color", SettingsTypes.ColorPicker, 13,
            Description = "Color of the steering input dot",
            ViewMode = SettingsViewMode.Expert)]
        public string SteeringInputColor
        {
            get => steeringInputColor;
            set
            {
                if (value == steeringInputColor) return;
                steeringInputColor = value;
                NotifyPropertyChanged();
            }
        }

        private bool showPedalValues = true;

        [SettingType("Show Pedal Values", SettingsTypes.Checkbox, 14,
        Description = "Show throttle/brake input values",
        ViewMode = SettingsViewMode.Beginner)]
        public bool ShowPedalValues
        {
            get => showPedalValues;
            set
            {
                if (value == showPedalValues) return;
                showPedalValues = value;
                NotifyPropertyChanged();
            }
        }

        private bool showClutch = true;

        [SettingType("Show Clutch", SettingsTypes.Checkbox, 15,
            Description = "Show clutch input",
            ViewMode = SettingsViewMode.Intermediate)]
        public bool ShowClutch
        {
            get => showClutch;
            set
            {
                if (value == showClutch) return;
                showClutch = value;
                NotifyPropertyChanged();
            }
        }

        private string clutchColor = "#ffdc1c";
        [SettingType("Clutch Color", SettingsTypes.ColorPicker, 20,
            Description = "Color of the clutch input",
            ViewMode = SettingsViewMode.Expert)]
        public string ClutchColor
        {
            get => clutchColor;
            set
            {
                if (value == clutchColor) return;
                clutchColor = value;
                NotifyPropertyChanged();
            }
        }

        private string throttleColor = "#1ea5ff";

        [SettingType("Throttle Color", SettingsTypes.ColorPicker, 21,
            Description = "Color of the throttle input",
            ViewMode = SettingsViewMode.Expert)]
        public string ThrottleColor
        {
            get => throttleColor;
            set
            {
                if (value == throttleColor) return;
                throttleColor = value;
                NotifyPropertyChanged();
            }
        }

        private string brakeColor = "#ff1e1e";

        [SettingType("Brake Color", SettingsTypes.ColorPicker, 22,
            Description = "Color of the brake input",
            ViewMode = SettingsViewMode.Expert)]
        public string BrakeColor
        {
            get => brakeColor;
            set
            {
                if (value == brakeColor) return;
                brakeColor = value;
                NotifyPropertyChanged();
            }
        }


        private bool showPedalDiff = true;
        [SettingType("Show diff of pedal input", SettingsTypes.Checkbox, 25,
            Description = "Show diff of pedal input and the ingame pedal input after TC and ABS",
            ViewMode = SettingsViewMode.Intermediate)]
        public bool ShowPedalDiff
        {
            get => showPedalDiff;
            set
            {
                if (value == showPedalDiff) return;
                showPedalDiff = value;
                NotifyPropertyChanged();
            }
        }

        private string diffColor = "000000bf";
        [SettingType("Pedal diff Color", SettingsTypes.ColorPicker, 26,
            Description = "Color when there is a difference between Pedal raw and actual pedal value",
            ViewMode = SettingsViewMode.Expert)]
        public string DiffColor
        {
            get => diffColor;
            set
            {
                if (value == diffColor) return;
                diffColor = value;
                NotifyPropertyChanged();
            }
        }

        private string wheelStyle = "default";

        public string WheelStyle
        {
            get => wheelStyle;
            set
            {
                if (value == wheelStyle) return;
                wheelStyle = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the path to the wheel SVG file based on the selected style
        /// </summary>
        public string WheelSvgPath => $"assets/img/wheels/wheel-{WheelStyle}.svg";
    }
}
