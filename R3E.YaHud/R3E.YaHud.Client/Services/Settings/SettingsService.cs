using Microsoft.JSInterop;
using R3E.YaHud.Client.Widget.Core;

namespace R3E.YaHud.Client.Services.Settings
{
    public class SettingsService(IJSRuntime js)
    {
        private IJSRuntime JS { get; set; } = js;

        private GlobalSettings? globalSettings;

        public GlobalSettings GlobalSettings
        {
            get => globalSettings!;
        }

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
            await Save(widget.ElementId, widget.Settings);
        }

        private async Task Save<TSettings>(string id, TSettings settings) where TSettings : new()
        {
            if (JS is null) return;
            try
            {
                await JS.InvokeVoidAsync("HudHelper.setWidgetSettings", id, settings);
            }
            catch (InvalidOperationException ex)
                    when (ex.Message.Contains("server-side static rendering"))
            {
                // Ignore during server-side prerendering
            }
        }

        public async Task SaveAll()
        {
            await Save(nameof(GlobalSettings), GlobalSettings);
            await Task.WhenAll(Widgets.Select(Save));
        }

        public async Task<TSettings?> Load<TSettings>(IWidget widget) where TSettings : BasicSettings, new()
        {
            globalSettings ??= await Load<GlobalSettings>(nameof(GlobalSettings)) ?? new GlobalSettings();

            return await Load<TSettings>(widget.ElementId);
        }

        private async Task<TSettings?> Load<TSettings>(string id) where TSettings : class
        {
            if (JS is null) return null;
            try
            {
                return await JS.InvokeAsync<TSettings>("HudHelper.getWidgetSettings", id);
            }
            catch (InvalidOperationException ex)
                    when (ex.Message.Contains("server-side static rendering"))
            {
                // Ignore during server-side prerendering
                return null;
            }
        }
    }
}
