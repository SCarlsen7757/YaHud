using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace R3E.YaHud.Client.Widget
{
    public abstract class HudWidgetBase : ComponentBase, IDisposable
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HudLockService LockService { get; set; } = default!;

        [JSInvokable]
        public bool GetLockState() => Locked;

        protected bool Locked => LockService.Locked;
        private DotNetObjectReference<HudWidgetBase>? objRef;
        private static bool lockServiceInitialized;

        protected abstract string ElementId { get; }
        public abstract string Name { get; }
        protected abstract double DefaultXPercent { get; } // center horizontally
        protected abstract double DefaultYPercent { get; } // center vertically

        protected abstract void Update();

        protected override void OnInitialized()
        {
            LockService.OnLockChanged += OnLockChanged;
        }


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                objRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("HudHelper.makeDraggable", ElementId, objRef);

                if (!lockServiceInitialized)
                {
                    await LockService.InitializeAsync(JS);
                    lockServiceInitialized = true;
                }
            }
        }

        private void OnLockChanged(bool newState)
        {
            InvokeAsync(StateHasChanged);
        }
        public void InvokeUpdate()
        {
            Update();
            InvokeAsync(StateHasChanged);
        }

        protected async Task ResetPosition()
        {
            await JS.InvokeVoidAsync("HudHelper.resetPosition", ElementId, DefaultXPercent, DefaultYPercent);
        }

        public virtual void Dispose()
        {
            LockService.OnLockChanged -= OnLockChanged;
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
