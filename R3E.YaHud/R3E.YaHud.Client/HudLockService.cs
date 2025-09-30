using R3E.YaHud.Client.Services;

namespace R3E.YaHud.Client
{
    public class HudLockService : IDisposable
    {
        private readonly ShortcutClientService shortcutService;

        public HudLockService(ShortcutClientService shortcutService)
        {
            this.shortcutService = shortcutService;
            this.shortcutService.ToggleLockShortcutReceived += OnLockShortcutReceived;
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
                OnLockChanged?.Invoke(locked);
            }
        }


        public void ToggleLock() => Locked = !Locked;

        public void SetLock(bool locked) => Locked = locked;

        public void Dispose()
        {
            shortcutService.ToggleLockShortcutReceived -= OnLockShortcutReceived;
            GC.SuppressFinalize(this);
        }
    }

}
