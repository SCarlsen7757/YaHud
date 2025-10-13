namespace R3E.YaHud.Components.Widget.Core
{
    public interface IWidget
    {
        public string Name { get; }
        public string Category { get; }
        public string ElementId { get; }

        public BasicSettings Settings { get; }
        public Type GetSettingsType();

        public void InvokeUpdate();

        public Task ResetPosition();

        public Task ClearSettings();
    }
}