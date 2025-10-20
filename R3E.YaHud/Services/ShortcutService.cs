using Microsoft.Extensions.Logging.Abstractions;
using SharpHook;
using SharpHook.Data;

namespace R3E.YaHud.Services
{
    public class ShortcutService : IDisposable
    {
        private readonly SimpleGlobalHook? hook;
        private bool disposed;
        private readonly ILogger<ShortcutService> logger;

        public event Action? ToggleLockShortcutReceived;

        public ShortcutService(ILogger<ShortcutService>? logger = null)
        {
            this.logger = logger ?? NullLogger<ShortcutService>.Instance;
            if (System.Diagnostics.Debugger.IsAttached) return;

            hook = new SimpleGlobalHook();
            hook.KeyPressed += OnKeyPressed;

            // Run hook safely
            _ = hook.RunAsync();
            logger.LogDebug("ShortcutService initialized");
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // Define the required modifier keys
            const EventMask requiredModifiers = EventMask.LeftShift | EventMask.LeftCtrl | EventMask.LeftAlt;

            // Check if the 'L' key is pressed with the required modifiers
            if (e.Data.KeyCode == KeyCode.VcL &&
                (e.RawEvent.Mask & requiredModifiers) == requiredModifiers)
            {
                logger.LogDebug("Toggle lock shortcut pressed");
                ToggleLockShortcutReceived?.Invoke();
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
