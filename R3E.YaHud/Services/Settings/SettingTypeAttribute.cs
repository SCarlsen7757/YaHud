using System.Collections.Concurrent;
using System.Reflection;

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

        // Global registry so predicates survive separate Attribute instances created by reflection
        private static readonly ConcurrentDictionary<string, Func<bool>> globalVisibilityPredicates = new();

        private static string KeyFor(PropertyInfo prop) =>
            $"{prop.DeclaringType?.FullName}.{prop.Name}";

        /// <summary>
        /// Register a visibility predicate for a specific property (declaring type + property name).
        /// Stored in a global registry so later reflection-created attribute instances can observe it.
        /// </summary>
        public void RegisterVisibilityPredicate(PropertyInfo prop, Func<bool> func)
        {
            if (prop == null) return;
            globalVisibilityPredicates[KeyFor(prop)] = func ?? (() => true);
        }

        /// <summary>
        /// Checks if this setting should be visible based on the visibility predicate.
        /// If a PropertyInfo is supplied, the global registry is checked first.
        /// </summary>
        public bool IsVisible(PropertyInfo? prop = null)
        {
            if (prop != null)
            {
                if (globalVisibilityPredicates.TryGetValue(KeyFor(prop), out var f))
                {
                    try { return f(); } catch { return true; }
                }
            }

            try { return VisibilityPredicate(); } catch { return true; }
        }
    }
}
