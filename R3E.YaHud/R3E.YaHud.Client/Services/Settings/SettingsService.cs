using Microsoft.JSInterop;
using R3E.YaHud.Client.Widget.Core;

namespace R3E.YaHud.Client.Services.Settings
{
    public class SettingsService(IJSRuntime js)
    {
        private IJSRuntime JS { get; set; } = js;

        public List<IWidget> Widgets { get; private set; } = [];
        public void RegisterWidget(IWidget widget)
        {
            if (Widgets.Any(w => w.Name == widget.Name && w.Category == widget.Category))
                throw new InvalidOperationException($"Widget with name '{widget.Name}' and category '{widget.Category}' is already registered.");
            Widgets.Add(widget);
        }

        public void UnregisterWidget(IWidget widget)
        {
            Widgets.Remove(widget);
        }

        public async Task Save(IWidget widget)
        {
            if (JS is null) return;
            try
            {
                await JS.InvokeVoidAsync("HudHelper.setWidgetSettings", widget.ElementId, widget.Settings);
            }
            catch (InvalidOperationException ex)
                    when (ex.Message.Contains("server-side static rendering"))
            {
                // Ignore during server-side prerendering
            }
        }

        public async Task SaveAll()
        {
            if (JS is null) return;
            await Task.WhenAll(Widgets.Select(Save));
        }

        public async Task<TSettings?> Load<TSettings>(IWidget widget) where TSettings : BasicSettings, new()
        {
            return await JS.InvokeAsync<TSettings>("HudHelper.getWidgetSettings", widget.ElementId);
        }
    }
}
