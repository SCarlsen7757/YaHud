using R3E.YaHud.Components.UI.Components.Settings;
using R3E.YaHud.Services.Settings;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace R3E.YaHud.Components.Widget.Core
{
    public class BasicSettings : INotifyPropertyChanged
    {
        public double XPercent { get; set; }
        public double YPercent { get; set; }

        private bool visible = true;

        //[SettingType("Visible", SettingsTypes.Checkbox, 0,
        //    Description = "Show or hide this widget")]
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

        public double Scale { get; set; } = 1.0;

        /// <summary>
        /// Whether this widget is pinned in place by its own CSS rather than positioned from
        /// <see cref="XPercent"/>/<see cref="YPercent"/>. Settings classes that expose a dock
        /// setting override this; everything else stays free.
        /// </summary>
        /// <remarks>
        /// It lives on the settings rather than on the widget so it can flip at runtime from a
        /// single settings change, and so <see cref="IsPositionSettingVisible"/> can be used as a
        /// <see cref="SettingTypeAttribute.VisibilityPredicateName"/> without any widget plumbing.
        /// </remarks>
        public virtual bool Docked => false;

        /// <summary>
        /// Visibility predicate for settings that only mean something when the widget can be moved.
        /// Scale is deliberately not covered: a docked widget still scales.
        /// </summary>
        public bool IsPositionSettingVisible() => !Docked;

        public event PropertyChangedEventHandler? PropertyChanged;


        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsPropertyVisible(SettingTypeAttribute attr)
        {
            
            if (string.IsNullOrEmpty(attr.VisibilityPredicateName)) return true;

            var method = this.GetType().GetMethod(attr!.VisibilityPredicateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0) return true;

            return method.CreateDelegate<Func<bool>>(this)();
        }
    }
}
