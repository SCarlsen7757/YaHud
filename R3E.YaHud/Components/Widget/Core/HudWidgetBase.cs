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
        [Inject] protected TestModeService TestModeService { get; set; } = default!;
        [Inject] protected ILogger<HudWidgetBase<TSettings>> Logger { get; set; } = default!;


        protected bool Locked => LockService.Locked;
        protected bool TestMode => TestModeService.TestMode;
        private DotNetObjectReference<HudWidgetBase<TSettings>>? objRef;

        public abstract string ElementId { get; }
        public abstract string Name { get; }
        public abstract string Category { get; }

        private bool visibleInitialized = false;

        public abstract double DefaultXPercent { get; }
        public abstract double DefaultYPercent { get; }

        public abstract bool Collidable { get; }

        BasicSettings IWidget.Settings => Settings;

        public TSettings Settings { get; set; } = new();
        public Type GetSettingsType() => typeof(TSettings);

        protected bool UseR3EData { get; set; } = true;
        protected TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        private DateTime lastUpdate = DateTime.MinValue;

        protected abstract void Update();

        protected abstract void UpdateWithTestData();

        protected override void OnInitialized()
        {
            SettingsService.RegisterWidget(this);
            LockService.OnLockChanged += OnLockChanged;
            TestModeService.OnTestModeChanged += OnTestModeChanged;
            if (UseR3EData) TelemetryService.DataUpdated += OnTelemetryDataUpdated;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Settings = await SettingsService.Load<TSettings>(this) ?? new() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
                Settings.PropertyChanged += Settings_PropertyChanged;
                await InvokeAsync(StateHasChanged);
            }

            if (Settings.Visible && (firstRender || !visibleInitialized))
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

        private void OnTestModeChanged(bool isTestMode)
        {
            InvokeUpdate();
        }

        public void InvokeUpdate()
        {
            if (!Settings.Visible) return;

            if (TestMode)
            {
                UpdateWithTestData();
            }
            else
            {
                Update();
            }

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
            try
            {
                // Guard against being called after component is disposed
                if (_disposed)
                {
                    Logger?.LogDebug("UpdateWidgetPosition called on disposed component (ElementId: {ElementId})", ElementId);
                    return;
                }

                if (Settings == null)
                {
                    Logger?.LogWarning("UpdateWidgetPosition called with null Settings (ElementId: {ElementId})", ElementId);
                    return;
                }

                Logger?.LogDebug("UpdateWidgetPosition called: {ElementId} xPercent={XPercent}, yPercent={YPercent}",
                    ElementId, xPercent, yPercent);

                Settings.XPercent = xPercent;
                Settings.YPercent = yPercent;
                await SettingsService.Save(this);

                Logger?.LogDebug("UpdateWidgetPosition saved successfully for {ElementId}", ElementId);
            }
            catch (ObjectDisposedException ex)
            {
                Logger?.LogDebug(ex, "Component disposed during UpdateWidgetPosition for {ElementId}", ElementId);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("disposed"))
            {
                Logger?.LogDebug(ex, "Widget {ElementId} is no longer available", ElementId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Unexpected error in UpdateWidgetPosition for widget {ElementId}", ElementId);
            }
        }

        [JSInvokable]
        public async Task OnWindowResize()
        {
            try
            {
                // Guard against being called after component is disposed
                if (_disposed || Settings == null)
                {
                    Logger?.LogDebug("OnWindowResize called on disposed component or with null settings");
                    return;
                }

                await JS.InvokeVoidAsync("HudHelper.setPosition", ElementId, Settings.XPercent, Settings.YPercent);
            }
            catch (JSDisconnectedException)
            {
                Logger?.LogDebug("JS interop failed: Circuit disconnected during OnWindowResize for widget {ElementId}", ElementId);
            }
            catch (JSException ex)
            {
                Logger?.LogWarning(ex, "JS interop error in OnWindowResize for widget {ElementId}", ElementId);
            }
            catch (ObjectDisposedException ex)
            {
                Logger?.LogDebug(ex, "Component disposed during OnWindowResize for widget {ElementId}", ElementId);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("disposed"))
            {
                Logger?.LogDebug(ex, "Widget {ElementId} is no longer available", ElementId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Unexpected error in OnWindowResize for widget {ElementId}", ElementId);
            }
        }

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SettingsService.UnregisterWidget(this);
            LockService.OnLockChanged -= OnLockChanged;
            TestModeService.OnTestModeChanged -= OnTestModeChanged;
            TelemetryService.DataUpdated -= OnTelemetryDataUpdated;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
