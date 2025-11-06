using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.UserInputs
{
    public class UserInputsSettings : BasicSettings
    {
        private bool showSteering = true;

        [SettingType("Show Steering input", SettingsTypes.Checkbox, 10,
            Description = "Show steering input",
            ViewMode = SettingsViewMode.Beginner)]
        public bool ShowSteering
        {
            get => showSteering;
            set
            {
                if (value == showSteering) return;
                showSteering = value;
                NotifyPropertyChanged();
            }
        }

        private string steeringColor = "#ffffff";

        [SettingType("Steering Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the steering input",
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
            Description = "Color of the RPM bar",
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
            Description = "Color of the RPM bar",
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
        public string WheelSvgPath => $"/img/wheels/wheel-{WheelStyle}.svg";
    }
}
