using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.CarDamage
{
    public class CarDamageSettings : BasicSettings
    {

        [SettingType("Pristine Color", SettingsTypes.ColorPicker, 10,
            Description = "The color of the bar when the car is in pristine state.",
            ViewMode = SettingsViewMode.Expert)]
        public string PristineColor { get; set; } = "#ffffff";

        [SettingType("Worn Threshold", SettingsTypes.Slider, 10,
            Description = "The threshold at which the car is considered worn.",
            Max = 1.0f,
            Min = 0.0f,
            Step = 0.01f,
            ViewMode = SettingsViewMode.Intermediate)]
        public float WornThreshold { get; set; } = 0.75f;

        [SettingType("Worn Color", SettingsTypes.ColorPicker, 10,
            Description = "The color of the bar when the car is in worn state.",
            ViewMode = SettingsViewMode.Expert)]
        public string WornColor { get; set; } = "#ffff00";

        [SettingType("Damaged Threshold", SettingsTypes.Slider, 10,
            Description = "The threshold at which the car is considered damaged.",
            Max = 1.0f,
            Min = 0.0f,
            Step = 0.01f,
            ViewMode = SettingsViewMode.Intermediate)]
        public float DamagedThreshold { get; set; } = 0.35f;

        [SettingType("Damaged Color", SettingsTypes.ColorPicker, 10,
            Description = "The color of the bar when the car is in damaged state.",
            ViewMode = SettingsViewMode.Expert)]

        public string DamagedColor { get; set; } = "#ffff0000";
    }
}
