using Microsoft.Extensions.Logging.Abstractions;

namespace R3E.YaHud.Services
{
    public class HudLockService : IDisposable
    {
        private readonly ShortcutService shortcutService;
        private readonly ILogger<HudLockService> logger;
        private bool disposed;

        public HudLockService(ShortcutService shortcutService, ILogger<HudLockService>? logger = null)
        {
            locked = !System.Diagnostics.Debugger.IsAttached;
            this.shortcutService = shortcutService;
            this.shortcutService.ToggleLockShortcutReceived += OnLockShortcutReceived;
            this.logger = logger ?? NullLogger<HudLockService>.Instance;
        }

        public event Action<bool>? OnLockChanged;

        public void OnLockShortcutReceived()
        {
            ToggleLock();
        }

        private bool locked = false;
        public bool Locked
        {
            get => locked;
            private set
            {
                if (locked == value) return;
                locked = value;
                logger.LogDebug("Lock state changed: {Locked}", locked);
                OnLockChanged?.Invoke(locked);
            }
        }


        public void ToggleLock() => Locked = !Locked;

        public void SetLock(bool locked) => Locked = locked;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            shortcutService.ToggleLockShortcutReceived -= OnLockShortcutReceived;
            GC.SuppressFinalize(this);
        }
    }
}
