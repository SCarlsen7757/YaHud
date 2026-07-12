namespace R3E.YaHud.Components.UI.Components.Settings
{
    public class SettingPanelEvents
    {
        public event Action? BodyClicked;

        public void RaiseBodyClick()
            => BodyClicked?.Invoke();
    }
}
