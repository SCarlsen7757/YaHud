using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;
using System.Reflection;

namespace R3E.YaHud.Components.Widget.StartLight
{
    public enum StartLightBehavior
    {
        /// <summary>Turn lights on one-by-one, then turn them all off.</summary>
        SequentialThenOff = 0,

        /// <summary>Turn lights on one-by-one, then show green (all on), then turn off.</summary>
        SequentialThenGreenThenOff = 1
    }

    public class StartLightSettings : BasicSettings
    {
        [SettingType("Light Behavior", SettingsTypes.Enum, 20)]
        public StartLightBehavior Behavior { get; set; } = StartLightBehavior.SequentialThenOff;

        [SettingType("Green light on duration (ms)", SettingsTypes.Slider, 21,
            Description = "Duration the green light stays on before turning off.",
            Max = 5000,
            Min = 1000,
            Step = 100,
            ViewMode = SettingsViewMode.Intermediate)]
        public int GreenLightDurationMs { get; set; } = 2500;

        private void UpdateVisibilityPredicate()
        {
            var prop = this.GetType().GetProperty(nameof(GreenLightDurationMs));
            if (prop != null)
            {
                var attr = prop.GetCustomAttribute<SettingTypeAttribute>();
                if (attr != null)
                {
                    attr.VisibilityPredicate = () => this.Behavior == StartLightBehavior.SequentialThenGreenThenOff;
                }
            }
        }

        public StartLightSettings()
        {
            UpdateVisibilityPredicate();
        }
    }
}
