using Microsoft.JSInterop;

namespace R3E.YaHud.Client
{
    public class HudLockService : IDisposable
    {
        private IJSRuntime? js;
        private DotNetObjectReference<HudLockService>? objRef;

        public Task InitializeAsync(IJSRuntime js)
        {
            this.js = js;
            objRef = DotNetObjectReference.Create(this);
            return this.js.InvokeVoidAsync("HudHelper.setupHotkey", objRef).AsTask();
        }

        private bool locked = false;
        public bool Locked
        {
            get => locked;
            private set
            {
                if (locked == value) return;
                locked = value;
                OnLockChanged?.Invoke(locked);
            }
        }

        public event Action<bool>? OnLockChanged;

        [JSInvokable]
        public void ToggleLock() => Locked = !Locked;

        [JSInvokable]
        public void SetLock(bool locked) => Locked = locked;

        public void Dispose()
        {
            objRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

}
