using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using R3E.Core.Interfaces;
using R3E.Core.Services;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;

namespace R3E.YaHud.Components.Widget.Core
{
    public abstract class HudWidgetBase<TSettings> : ComponentBase, IDisposable, IWidget where TSettings : BasicSettings, new()
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HudLockService LockService { get; set; } = default!;
        [Inject] protected ITelemetryService TelemetryService { get; set; } = default!;
        [Inject] protected SettingsService SettingsService { get; set; } = default!;
        [Inject] protected TestModeService TestModeService { get; set; } = default!;
        [Inject] protected ILogger<HudWidgetBase<TSettings>> Logger { get; set; } = default!;


        protected bool Locked => LockService.Locked;
        protected bool TestMode => TestModeService.TestMode;
        private DotNetObjectReference<HudWidgetBase<TSettings>>? objRef;

        public ElementReference ElementRef { get; set; }
        public abstract string ElementId { get; }
        public abstract string Name { get; }
        public abstract string Category { get; }


        public abstract double DefaultXPercent { get; }
        public abstract double DefaultYPercent { get; }

        public abstract bool Collidable { get; }

        /// <summary>
        /// Whether the widget is pinned in place by its own CSS instead of by
        /// <see cref="BasicSettings.XPercent"/>/<see cref="BasicSettings.YPercent"/>.
        /// </summary>
        /// <remarks>
        /// A docked widget gets no drag listener, which is the point: with nothing native listening
        /// for <c>mousedown</c> on the host, controls inside the widget keep working while the HUD
        /// is unlocked. It also takes no part in collision or snapping, and its position settings are
        /// meaningless. Scale is unaffected. Derived from the settings so it can flip at runtime.
        /// </remarks>
        public bool Docked => Settings?.Docked ?? false;

        /// <summary>
        /// Transform origin a docked widget is scaled about, so growing the scale never pushes the
        /// widget off the edge it is pinned to. Defaults to the free-widget origin.
        /// </summary>
        protected virtual string DockedTransformOrigin => "top left";

        BasicSettings? IWidget.Settings => Settings;

        public TSettings? Settings { get; set; }
        public Type GetSettingsType() => typeof(TSettings);

        protected virtual bool UseR3EData { get; set; } = true;
        protected TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        private DateTime lastUpdate = DateTime.MinValue;
        private bool initializedTransformations = false;
        private bool registeredTransformations = false;
        private bool? appliedDocked;

        protected virtual void Update() { }

        /// <summary>
        /// Async update hook. Runs on the Blazor dispatcher via <see cref="InvokeUpdate"/>.
        /// The default implementation calls the synchronous <see cref="Update"/>; widgets that
        /// need to await work (e.g. HTTP lookups) should override this instead of using async void.
        /// </summary>
        protected virtual Task UpdateAsync()
        {
            Update();
            return Task.CompletedTask;
        }

        protected abstract void UpdateWithTestData();

        protected virtual Task OnSettingsLoadedAsync() => Task.CompletedTask;

        protected override void OnInitialized()
        {
            SettingsService.RegisterWidget(this);
            LockService.OnLockChanged += OnLockChanged;
            TestModeService.OnTestModeChanged += OnTestModeChanged;
            if (UseR3EData)
            {
                TelemetryService.DataUpdated += OnTelemetryDataUpdated;
                TelemetryService.TelemetryReset += OnTelemetryResetRaised;
            }
        }

        /// <summary>
        /// Called when accumulated telemetry state becomes invalid - session change, session
        /// restart, RaceRoom replay, or a rewound stream. Only widgets that keep their own history
        /// across frames need to override; everything derived from a feature service is reset by
        /// that service. Raised on the telemetry thread, so marshal any render onto the dispatcher.
        /// </summary>
        protected virtual void OnTelemetryReset() { }

        private void OnTelemetryResetRaised(TelemetryData data)
        {
            try
            {
                OnTelemetryReset();
            }
            catch (Exception ex)
            {
                // A throwing widget must not break the reset for the widgets after it in the
                // invocation list, nor fault the telemetry thread.
                Logger.LogError(ex, "Error resetting widget {ElementId}", ElementId);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Settings = await SettingsService.Load<TSettings>(this) ?? new() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
                Settings.PropertyChanged += Settings_PropertyChanged;
                await OnSettingsLoadedAsync();

                StateHasChanged();
                return;
            }


            if (!(Settings?.Visible ?? false))
            {
                initializedTransformations = false;
                await JS.InvokeVoidAsync("HudHelper.disableTransformation", ElementId);
                return;
            }

            // Docking can be switched from the settings panel, so both the registration (which
            // decides whether a drag listener exists) and the placement have to be redone when it
            // changes - not just once on first render.
            var docked = Docked;
            if (appliedDocked != docked)
            {
                registeredTransformations = false;
                initializedTransformations = false;
            }

            if (!registeredTransformations)
            {
                registeredTransformations = true;
                try
                {
                    objRef ??= DotNetObjectReference.Create(this);

                    await JS.InvokeVoidAsync(
                        "HudHelper.registerTransformable",
                        ElementId,
                        objRef,
                        Locked,
                        // Docked widgets are placed by CSS and never move, so they cannot collide
                        // with anything or be snapped away from their edge.
                        Collidable && !docked,
                        !docked
                    );

                }
                catch (TaskCanceledException)
                {
                    // Expected if component is disposed mid-render
                }
            }

            if (ElementRef.Context is not null && !initializedTransformations)
            {
                initializedTransformations = true;
                appliedDocked = docked;

                await JS.InvokeVoidAsync(
                    "HudHelper.setScale",
                    ElementId,
                    Settings.Scale
                );

                if (docked)
                {
                    // Drops whatever inline placement a previous free position (or drag) left on the
                    // element, so the docking rules in the stylesheet are what actually applies.
                    await JS.InvokeVoidAsync("HudHelper.dockElement", ElementId, DockedTransformOrigin);
                }
                else
                {
                    await JS.InvokeVoidAsync(
                        "HudHelper.setPosition",
                        ElementId,
                        Settings.XPercent,
                        Settings.YPercent
                    );
                }

                await JS.InvokeVoidAsync("HudHelper.enableTransformation", ElementId);
            }
        }

        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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
                        await JS.InvokeVoidAsync("HudHelper.disableTransformation", ElementId);
                    else
                        await JS.InvokeVoidAsync("HudHelper.enableTransformation", ElementId);

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
            if (Settings == null || !Settings.Visible) return;

            // Telemetry updates arrive on the UDP receive thread. Marshal the state mutation,
            // any async work, and the render onto the Blazor dispatcher so the renderer never
            // diffs a tree that is being mutated on another thread (the "insertBefore/parentNode
            // is null" circuit crash), and so a fault stays contained to this widget instead of
            // taking down the host (and the co-hosted UDP receiver) via an unhandled exception.
            _ = InvokeAsync(async () =>
            {
                try
                {
                    if (TestMode)
                    {
                        UpdateWithTestData();
                    }
                    else
                    {
                        await UpdateAsync();
                    }

                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error updating widget {ElementId}", ElementId);
                }
            });
        }

        public async Task ResetPosition()
        {
            // The menu hides this for docked widgets; the guard keeps a stray call from stamping an
            // inline position onto an element the stylesheet is placing.
            if (Docked) return;

            objRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("HudHelper.resetPosition", ElementId, objRef, DefaultXPercent, DefaultYPercent);
        }

        public async Task ResetScale()
        {
            objRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("HudHelper.resetScale", ElementId, objRef);
        }

        public async Task ResetProperties()
        {
            if (Settings != null)
                Settings.PropertyChanged -= Settings_PropertyChanged;

            Settings = new TSettings() { XPercent = Settings!.XPercent, YPercent = Settings!.YPercent, Scale = Settings!.Scale };
            Settings.PropertyChanged += Settings_PropertyChanged;

            await SettingsService.Clear(this);
            await InvokeAsync(StateHasChanged);
        }

        public async Task ClearSettings()
        {
            if (Settings != null)
                Settings.PropertyChanged -= Settings_PropertyChanged;

            Settings = new TSettings() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
            Settings.PropertyChanged += Settings_PropertyChanged;

            await SettingsService.Clear(this);
            await InvokeAsync(StateHasChanged);
        }

        [JSInvokable]
        public async Task UpdateWidgetPosition(double xPercent, double yPercent)
        {
            try
            {
                // Guard against being called after component is disposed
                if (disposed)
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
        public async Task UpdateWidgetScale(double scale)
        {
            try
            {
                // Guard against being called after component is disposed
                if (disposed)
                {
                    Logger?.LogDebug("UpdateWidgetScale called on disposed component (ElementId: {ElementId})", ElementId);
                    return;
                }

                if (Settings == null)
                {
                    Logger?.LogWarning("UpdateWidgetScale called with null Settings (ElementId: {ElementId})", ElementId);
                    return;
                }

                Logger?.LogDebug("UpdateWidgetScale called: {ElementId} scale={scale}",
                    ElementId, scale);

                Settings.Scale = scale;
                await SettingsService.Save(this);

                Logger?.LogDebug("UpdateWidgetScale saved successfully for {ElementId}", ElementId);
            }
            catch (ObjectDisposedException ex)
            {
                Logger?.LogDebug(ex, "Component disposed during UpdateWidgetScale for {ElementId}", ElementId);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("disposed"))
            {
                Logger?.LogDebug(ex, "Widget {ElementId} is no longer available", ElementId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Unexpected error in UpdateWidgetScale for widget {ElementId}", ElementId);
            }
        }

        [JSInvokable]
        public async Task OnWindowResize()
        {
            try
            {
                // Guard against being called after component is disposed
                if (disposed || Settings == null)
                {
                    Logger?.LogDebug("OnWindowResize called on disposed component or with null settings");
                    return;
                }

                // A docked widget follows the viewport through CSS; re-stamping a percentage
                // position would tear it off its edge.
                if (Docked) return;

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
                Logger?.LogDebug(ex, "Component disposed during OnWindowResize for {ElementId}", ElementId);
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

        protected volatile bool disposed;

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;

            SettingsService.UnregisterWidget(this);
            LockService.OnLockChanged -= OnLockChanged;
            TestModeService.OnTestModeChanged -= OnTestModeChanged;
            TelemetryService.DataUpdated -= OnTelemetryDataUpdated;
            TelemetryService.TelemetryReset -= OnTelemetryResetRaised;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
