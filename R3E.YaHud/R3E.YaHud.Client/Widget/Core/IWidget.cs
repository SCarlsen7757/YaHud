
namespace R3E.YaHud.Client.Widget.Core
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