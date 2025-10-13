using R3E.YaHud.Services.Settings;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
