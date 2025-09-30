using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using R3E.YaHud.Client.Services;

namespace R3E.YaHud.Client.Widget
{
    public abstract class HudWidgetBase : ComponentBase, IDisposable
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HudLockService LockService { get; set; } = default!;
        [Inject] protected SharedMemoryClientService SharedMemoryClientService { get; set; } = default!;
        [Inject] protected SettingsService SettingsService { get; set; } = default!;

        protected bool Locked => LockService.Locked;
        private DotNetObjectReference<HudWidgetBase>? objRef;

        public abstract string ElementId { get; }
        public abstract string Name { get; }
        public abstract string Catagory { get; }
        private bool visible = true;
        private bool visibleInitialized = false;
        public bool Visible
        {
            get => visible;
            set
            {
                if (value == visible) return;
                visible = value;
                visibleInitialized = false;

                InvokeAsync(StateHasChanged); // Force re-render so OnAfterRenderAsync runs
            }
        }

        public double DefaultXPercent { get; protected set; }
        public double? XPercent { get; protected set; }
        public double DefaultYPercent { get; protected set; }
        public double? YPercent { get; protected set; }

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
            if (Visible && firstRender || !visibleInitialized)
            {
                var settings = await SettingsService.Load(JS, this);
                await JS.InvokeVoidAsync("HudHelper.setPosition", ElementId, settings.XPercent, settings.YPercent);

                visibleInitialized = true;
                objRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("HudHelper.makeDraggable", ElementId, objRef);
            }
        }

        [JSInvokable]
        public bool GetLockState() => Locked;

        private void OnLockChanged(bool newState)
        {
            InvokeAsync(StateHasChanged);
        }

        protected virtual void OnR3EDataUpdated(R3E.Data.Shared newData)
        {
            if (DateTime.Now - lastUpdate < UpdateInterval) return;
            lastUpdate = DateTime.Now;
            InvokeUpdate();
        }

        public void InvokeUpdate()
        {
            if (!Visible) return;
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
            XPercent = xPercent;
            YPercent = yPercent;
            await SettingsService.Save(JS, this);
        }

        public virtual void Dispose()
        {
            SettingsService.UnregisterWidget(this);
            LockService.OnLockChanged -= OnLockChanged;
            SharedMemoryClientService.DataUpdated -= OnR3EDataUpdated;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
