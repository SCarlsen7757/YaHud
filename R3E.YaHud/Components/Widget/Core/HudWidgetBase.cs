using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using R3E.API;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.Core
{
    public abstract class HudWidgetBase<TSettings> : ComponentBase, IDisposable, IWidget where TSettings : BasicSettings, new()
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HudLockService LockService { get; set; } = default!;
        [Inject] protected TelemetryService TelemetryService { get; set; } = default!;
        [Inject] protected SettingsService SettingsService { get; set; } = default!;
        [Inject] protected ILogger<HudWidgetBase<TSettings>> Logger { get; set; } = default!;


        protected bool Locked => LockService.Locked;
        private DotNetObjectReference<HudWidgetBase<TSettings>>? objRef;

        public abstract string ElementId { get; }
        public abstract string Name { get; }
        public abstract string Category { get; }

        private bool visibleInitialized = false;

        public abstract double DefaultXPercent { get; }
        public abstract double DefaultYPercent { get; }

        public abstract bool Collidable { get; }

        BasicSettings IWidget.Settings => Settings;

        public TSettings? Settings { get; set; } 
        public Type GetSettingsType() => typeof(TSettings);

        protected bool UseR3EData { get; set; } = true;
        protected TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        private DateTime lastUpdate = DateTime.MinValue;

        protected abstract void Update();

        protected override void OnInitialized()
        {
            SettingsService.RegisterWidget(this);
            LockService.OnLockChanged += OnLockChanged;
            if (UseR3EData) TelemetryService.DataUpdated += OnTelemetryDataUpdated;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Settings = await SettingsService.Load<TSettings>(this) ?? new() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
                Settings.PropertyChanged += Settings_PropertyChanged;
                Settings.InitializeVisibilityPredicates();
                await InvokeAsync(StateHasChanged);
            }

            if (Settings.Visible && firstRender || !visibleInitialized)
            {
                await JS.InvokeVoidAsync("HudHelper.setPosition", ElementId, Settings.XPercent, Settings.YPercent);

                visibleInitialized = true;
                objRef ??= DotNetObjectReference.Create(this);
                // Register draggable and pass current lock state to decide if handlers are attached
                await JS.InvokeVoidAsync("HudHelper.registerDraggable", ElementId, objRef, Locked, Collidable);
            }
        }

        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BasicSettings.Visible))
            {
                if (Settings.Visible)
                {
                    visibleInitialized = false;
                }
            }
            InvokeAsync(StateHasChanged);
        }

        [JSInvokable]
        public bool GetLockState() => Locked;

        private void OnLockChanged(bool newState)
        {
            // Use InvokeAsync directly - no need for Task.Run
            // InvokeAsync already handles the threading context properly
            _ = InvokeAsync(async () =>
            {
                try
                {
                    if (newState)
                        await JS.InvokeVoidAsync("HudHelper.disableDragging", ElementId);
                    else
                        await JS.InvokeVoidAsync("HudHelper.enableDragging", ElementId);

                    StateHasChanged();
                }
                catch (JSDisconnectedException)
                {
                    // Expected when circuit is disconnected - safe to ignore
                    Logger.LogDebug("JS interop failed: Circuit disconnected for widget {ElementId}", ElementId);
                }
                catch (JSException ex)
                {
                    // Log JS errors but don't crash - UI updates are non-critical
                    Logger.LogWarning(ex, "JS interop error updating drag state for widget {ElementId}", ElementId);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("disposed"))
                {
                    // Circuit disconnected or component disposed
                    Logger.LogDebug(ex, "Widget {ElementId} is no longer available", ElementId);
                }
            });
        }

        protected virtual void OnTelemetryDataUpdated(TelemetryData newData)
        {
            if (DateTime.Now - lastUpdate < UpdateInterval) return;
            lastUpdate = DateTime.Now;
            InvokeUpdate();
        }

        public void InvokeUpdate()
        {
            if (!Settings?.Visible ?? true) return;
            Update();
            InvokeAsync(StateHasChanged);
        }

        public async Task ResetPosition()
        {
            await JS.InvokeVoidAsync("HudHelper.resetPosition", ElementId, DefaultXPercent, DefaultYPercent);
        }

        public async Task ClearSettings()
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;

            Settings = new TSettings() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
            Settings.PropertyChanged += Settings_PropertyChanged;
            visibleInitialized = false;

            await SettingsService.Clear(this);
            await InvokeAsync(StateHasChanged);
        }

        [JSInvokable]
        public async Task UpdateWidgetPosition(double xPercent, double yPercent)
        {
            Settings.XPercent = xPercent;
            Settings.YPercent = yPercent;
            await SettingsService.Save(this);
        }

        [JSInvokable]
        public async Task OnWindowResize()
        {
            await JS.InvokeVoidAsync("HudHelper.setPosition", ElementId, Settings.XPercent, Settings.YPercent);
        }

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SettingsService.UnregisterWidget(this);
            LockService.OnLockChanged -= OnLockChanged;
            TelemetryService.DataUpdated -= OnTelemetryDataUpdated;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
