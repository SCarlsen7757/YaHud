using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.LapProgress
{
    public class LapProgressSettings : BasicSettings
    {
        [SettingType("Positive Negative Bar Range", SettingsTypes.Number, 10,
            Min = 0.1, Max = 10, Step = 0.01,
            Description = "The value which maps to a 100% extended bar in either direction.",
            ViewMode = SettingsViewMode.Intermediate)]
        public double PositiveNegativeBarRange { get; set; } = 2;

        [SettingType("Positive Delta Time Color", SettingsTypes.ColorPicker, 11,
            Description = "Color of the delta time, when faster than previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string PositiveDeltaTimeColor { get; set; } = "#1BB12F";

        [SettingType("Negative Delta Time Color", SettingsTypes.ColorPicker, 12,
            Description = "Color of the delta time, when slower than previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NegativeDeltaTimeColor { get; set; } = "#b11b1b";

        [SettingType("Neutral Delta Time Color", SettingsTypes.ColorPicker, 13,
            Description = "Color of the delta time, when equal to previous time.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NeutralDeltaTimeColor { get; set; } = "#ffffff";

        [SettingType("Fastest Sector Time All Color", SettingsTypes.ColorPicker, 14,
            Description = "Color of the sector, when the sector time is the fastest time of all players.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string FastestSectorTimeAllColor { get; set; } = "#700f71";

        [SettingType("Fastest Sector Time Self Color", SettingsTypes.ColorPicker, 15,
            Description = "Color of the sector, when the sector time is the fastest sector of the player.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string FastestSectorTimeSelfColor { get; set; } = "#1a510b";

        [SettingType("Neutral Sector Time Color", SettingsTypes.ColorPicker, 16,
            Description = "Color of neutral sector times.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string NeutralSectorTimeColor { get; set; } = "#7e7e81";

        [SettingType("Completed Lap Sector Times Visible Time", SettingsTypes.Number, 17,
            Min = 0, Max = 10, Step = 1,
            Description = "The number of seconds, the sector times for a completed lap is visible before resetting.",
            ViewMode = SettingsViewMode.Beginner)]
        public double CompletedLapSectorTimesVisibleTime { get; set; } = 5;

        [SettingType("Lap Distance Bar Color", SettingsTypes.ColorPicker, 18,
            Description = "The color of the lap distance bar.",
            ViewMode = SettingsViewMode.Intermediate)]
        public string LapDistanceBarColor { get; set; } = "#ffffff";

        [SettingType("Sector Time and Info Text Color", SettingsTypes.ColorPicker, 19,
            Description = "The color of the sector time text and the info text underneath.",
            ViewMode = SettingsViewMode.Expert)]
        public string SectorTimeAndInfoTextColor { get; set; } = "#ffffff";
    }
}
