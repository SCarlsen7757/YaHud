using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using R3E.YaHud.Client.Services;
using R3E.YaHud.Client.Services.Settings;

namespace R3E.YaHud.Client.Widget.Core
{
    public abstract class HudWidgetBase<TSettings> : ComponentBase, IDisposable, IWidget where TSettings : BasicSettings, new()
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HudLockService LockService { get; set; } = default!;
        [Inject] protected SharedMemoryClientService SharedMemoryClientService { get; set; } = default!;
        [Inject] protected SettingsService SettingsService { get; set; } = default!;

        protected bool Locked => LockService.Locked;
        private DotNetObjectReference<HudWidgetBase<TSettings>>? objRef;

        public abstract string ElementId { get; }
        public abstract string Name { get; }
        public abstract string Category { get; }

        private bool visibleInitialized = false;

        public double DefaultXPercent { get; protected set; }
        public double DefaultYPercent { get; protected set; }

        BasicSettings IWidget.Settings => Settings;

        public TSettings Settings { get; set; } = new();
        public Type GetSettingsType() => typeof(TSettings);

        protected bool UseR3EData { get; set; } = true;
        protected TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        private DateTime lastUpdate = DateTime.MinValue;

        protected abstract void Update();

        protected override void OnInitialized()
        {
            SettingsService.RegisterWidget(this);
            LockService.OnLockChanged += OnLockChanged;
            if (UseR3EData) SharedMemoryClientService.DataUpdated += OnR3EDataUpdated;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Settings = await SettingsService.Load<TSettings>(this) ?? new() { XPercent = DefaultXPercent, YPercent = DefaultYPercent };
                Settings.PropertyChanged += Settings_PropertyChanged;
                _ = InvokeAsync(StateHasChanged);
            }

            if (Settings.Visible && firstRender || !visibleInitialized)
            {
                await JS.InvokeVoidAsync("HudHelper.setPosition", ElementId, Settings.XPercent, Settings.YPercent);

                visibleInitialized = true;
                objRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("HudHelper.makeDraggable", ElementId, objRef);
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
                InvokeAsync(StateHasChanged);
            }
        }

        [JSInvokable]
        public bool GetLockState() => Locked;

        private void OnLockChanged(bool newState)
        {
            InvokeAsync(StateHasChanged);
        }

        protected virtual void OnR3EDataUpdated(Data.Shared newData)
        {
            if (DateTime.Now - lastUpdate < UpdateInterval) return;
            lastUpdate = DateTime.Now;
            InvokeUpdate();
        }

        public void InvokeUpdate()
        {
            if (!Settings.Visible) return;
            Update();
            InvokeAsync(StateHasChanged);
        }

        public async Task ResetPosition()
        {
            await JS.InvokeVoidAsync("HudHelper.resetPosition", ElementId, DefaultXPercent, DefaultYPercent);
        }

        [JSInvokable]
        public async Task UpdateWidgetPosition(double xPercent, double yPercent)
        {
            Settings.XPercent = xPercent;
            Settings.YPercent = yPercent;
            await SettingsService.Save(this);
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SettingsService.UnregisterWidget(this);
            LockService.OnLockChanged -= OnLockChanged;
            SharedMemoryClientService.DataUpdated -= OnR3EDataUpdated;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
