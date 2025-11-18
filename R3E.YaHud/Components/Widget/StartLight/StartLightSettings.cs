using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;
using System.ComponentModel.DataAnnotations;

namespace R3E.YaHud.Components.Widget.StartLight
{
    public enum StartLightBehavior
    {
        /// <summary>Turn lights on one-by-one, then turn them all off (hide all lights).</summary>
        [Display(Name = "Red seq then off")]
        SequentialThenOff = 0,

        /// <summary>Turn lights on one-by-one, then show green (all on), then turn off.</summary>
        [Display(Name = "Red seq then green then off")]
        SequentialThenGreenThenOff = 1
    }

    public class StartLightSettings : BasicSettings
    {
        [SettingType("Light Behavior", SettingsTypes.Enum, 20,
            Description = "Light on/off sequence behavior",
            ViewMode = SettingsViewMode.Beginner)]
        public StartLightBehavior Behavior { get; set; } = StartLightBehavior.SequentialThenOff;

        [SettingType("Green light on duration (ms)", SettingsTypes.Slider, 21,
            Description = "Duration the green light stays on before turning off.",
            Max = 5000,
            Min = 1000,
            Step = 100,
            ViewMode = SettingsViewMode.Intermediate,
            VisibilityPredicateName = nameof(ShowGreenLightDuration))]
        public int GreenLightDurationMs { get; set; } = 2500;

        private bool ShowGreenLightDuration()
        {
            return Behavior == StartLightBehavior.SequentialThenGreenThenOff;
        }
    }
}
