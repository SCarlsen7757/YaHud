using Microsoft.AspNetCore.Components;

namespace R3E.YaHud.Components.Widget.Core
{
    public interface IWidget
    {
        public string Name { get; }
        public string Category { get; }
        public string ElementId { get; }
        public ElementReference ElementRef { get; set; }

        /// <summary>
        /// Whether the widget is pinned in place by its own CSS. Docked widgets cannot be dragged
        /// or repositioned, so the settings UI hides everything that only applies to a free widget.
        /// </summary>
        public bool Docked { get; }

        public BasicSettings? Settings { get; }
        public Type GetSettingsType();

        public void InvokeUpdate();

        public Task ResetPosition();
        public Task ResetScale();
        public Task ResetProperties();

        public Task ClearSettings();
    }
}