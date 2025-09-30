using Microsoft.JSInterop;
using R3E.YaHud.Client.Widget;

namespace R3E.YaHud.Client.Services
{
    public class SettingsService
    {
        public List<HudWidgetBase> Widgets { get; private set; } = [];
        public void RegisterWidget(HudWidgetBase widget)
        {
            if (Widgets.Any(w => w.Name == widget.Name && w.Catagory == widget.Catagory))
                throw new InvalidOperationException($"Widget with name '{widget.Name}' and category '{widget.Catagory}' is already registered.");
            Widgets.Add(widget);
        }

        public void UnregisterWidget(HudWidgetBase widget)
        {
            Widgets.Remove(widget);
        }

        public static async Task Save(IJSRuntime js, HudWidgetBase widget)
        {
            var settings = new Widget.BasicSettings() { XPercent = widget.XPercent ?? widget.DefaultXPercent, YPercent = widget.YPercent ?? widget.DefaultYPercent, Visible = widget.Visible };
            await js.InvokeVoidAsync("HudHelper.setWidgetSettings", widget.ElementId, settings);
        }

        public static async Task<Widget.BasicSettings> Load(IJSRuntime js, HudWidgetBase widget)
        {
            return await js.InvokeAsync<Widget.BasicSettings>("HudHelper.getWidgetSettings", widget.ElementId) ?? new() { Visible = true, XPercent = widget.DefaultXPercent, YPercent = widget.DefaultYPercent };
        }
    }
}
