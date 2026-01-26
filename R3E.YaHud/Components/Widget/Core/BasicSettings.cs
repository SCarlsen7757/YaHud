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

        public event PropertyChangedEventHandler? PropertyChanged;


        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsPropertyVisible(SettingInfo settingInfo)
        {
            //if (settingInfo.Prop.Name == nameof(Visible)) return false;
            
            if (string.IsNullOrEmpty(settingInfo.Attr!.VisibilityPredicateName)) return true;

            var method = this.GetType().GetMethod(settingInfo.Attr!.VisibilityPredicateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0) return true;

            return method.CreateDelegate<Func<bool>>(this)();
        }
    }
}
