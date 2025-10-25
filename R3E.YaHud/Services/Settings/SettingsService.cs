using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Services.Settings
{
    public class SettingsService(IJSRuntime js, ILogger<SettingsService>? logger = null)
    {
        private IJSRuntime JS { get; set; } = js;
        private readonly ILogger<SettingsService> logger = logger ?? NullLogger<SettingsService>.Instance;

        private GlobalSettings globalSettings = new();

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
                logger.LogDebug("Saved settings for {Id}", id);
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
                var result = await JS.InvokeAsync<TSettings>("HudHelper.getWidgetSettings", id);
                logger.LogDebug("Loaded settings for {Id}", id);
                return result;
            }
            catch (InvalidOperationException ex)
                    when (ex.Message.Contains("server-side static rendering"))
            {
                // Ignore during server-side prerendering
                return null;
            }
        }

        public async Task ClearAll()
        {
            await Clear(nameof(GlobalSettings));
            foreach (var widget in Widgets)
            {
                await widget.ClearSettings();
            }
            await JS.InvokeVoidAsync("location.reload");
            logger.LogDebug("Cleared all settings");
        }

        public async Task Clear(IWidget widget)
        {
            await Clear(widget.ElementId);
        }

        private async Task Clear(string id)
        {
            if (JS is null) return;
            try
            {
                await JS.InvokeVoidAsync("HudHelper.clearWidgetSettings", id);
                logger.LogDebug("Cleared settings for {Id}", id);
            }
            catch (InvalidOperationException ex)
                    when (ex.Message.Contains("server-side static rendering"))
            {
                // Ignore during server-side prerendering
            }
        }
    }
}
