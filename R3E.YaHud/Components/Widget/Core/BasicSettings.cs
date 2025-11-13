using R3E.YaHud.Services.Settings;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace R3E.YaHud.Components.Widget.Core
{
    public class BasicSettings : INotifyPropertyChanged
    {
        public double XPercent { get; set; }
        public double YPercent { get; set; }

        private bool visible = true;

        [SettingType("Visible", SettingsTypes.Checkbox, 0,
            Description = "Show or hide this widget")]
        public bool Visible
        {
            get => visible;
            set
            {
                if (value == visible) return;
                visible = value;
                NotifyPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonConstructor]
        public BasicSettings()
        {
            InitializeVisibilityPredicates();
        }

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Override this method in derived classes to initialize visibility predicates.
        /// This will be called both from constructor and after deserialization.
        /// </summary>
        public virtual void InitializeVisibilityPredicates()
        {
            // Base implementation does nothing
            // Derived classes override to set up their predicates
        }

        protected void AddVisibilityPredicate(string propertyName, Func<bool> func)
        {
            var propInfo = this.GetType().GetProperty(propertyName);
            if (propInfo == null) return;

            var attr = propInfo.GetCustomAttribute<SettingTypeAttribute>();
            if (attr == null) return;

            // Register predicate in attribute/global registry keyed by property (survives new attribute instances)
            attr.RegisterVisibilityPredicate(propInfo, func);
        }

        /// <summary>
        /// Called by System.Text.Json after all properties are set during deserialization.
        /// </summary>
        [JsonExtensionData]
        private Dictionary<string, object>? _extensionData;

        // This is a workaround: We use a property setter that's called after deserialization
        // Since JsonExtensionData is processed last, we can hook into it
        private bool _initialized;

        [JsonInclude]
        [JsonPropertyName("_init")]
        private bool InitializationTrigger
        {
            get => _initialized;
            set
            {
                _initialized = value;
                // This runs after deserialization is complete
                if (!_initialized)
                {
                    _initialized = true;
                    InitializeVisibilityPredicates();
                }
            }
        }
    }
}
