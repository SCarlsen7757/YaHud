namespace R3E.YaHud.Services.Settings
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingTypeAttribute(string displayName, SettingsTypes type, uint order) : Attribute
    {
        public string DisplayName { get; } = displayName;
        public SettingsTypes Type { get; } = type;

        public uint Order { get; } = order;
        public string? Description { get; set; }

        public SettingsViewMode ViewMode { get; set; } = SettingsViewMode.Beginner;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 100;
        public double Step { get; set; } = 1;

        /// <summary>
        /// Custom predicate function for visibility logic.
        /// If set, this determines whether the setting should be visible.
        /// Returns true if visible, false if hidden.
        /// </summary>
        public Func<bool> VisibilityPredicate { get; set; } = () => true;

        /// <summary>
        /// Checks if this setting should be visible based on the visibility predicate.
        /// </summary>
        public bool IsVisible() => VisibilityPredicate();
    }
}
