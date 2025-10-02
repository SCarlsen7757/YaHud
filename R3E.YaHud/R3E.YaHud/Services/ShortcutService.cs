using R3E.YaHud.Client.Services;
using SharpHook;
using SharpHook.Data;

namespace R3E.YaHud.Services
{
    public class ShortcutService : IDisposable
    {
        private readonly SimpleGlobalHook? hook;
        private bool disposed;

        public event Action<ShortcutClientService.Shortcut>? ShortcutPressed;

        public ShortcutService()
        {
            if (System.Diagnostics.Debugger.IsAttached) return;

            hook = new SimpleGlobalHook();
            hook.KeyPressed += OnKeyPressed;

            // Run hook safely
            _ = hook.RunAsync();
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // Define the required modifier keys
            const EventMask requiredModifiers = EventMask.LeftShift | EventMask.LeftCtrl | EventMask.LeftAlt;

            // Check if the 'L' key is pressed with the required modifiers
            if (e.Data.KeyCode == KeyCode.VcL &&
                (e.RawEvent.Mask & requiredModifiers) == requiredModifiers)
            {
                ShortcutPressed?.Invoke(ShortcutClientService.Shortcut.ToggleLock);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (hook is not null)
            {
                hook.KeyPressed -= OnKeyPressed;
                hook.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
