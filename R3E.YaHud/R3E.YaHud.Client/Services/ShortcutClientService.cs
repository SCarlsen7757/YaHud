using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace R3E.YaHud.Client.Services
{
    public class ShortcutClientService
    {
        public enum Shortcut
        {
            ToggleLock
        }

        private readonly HubConnection connection;
        public event Action? ToggleLockShortcutReceived;

        public ShortcutClientService(NavigationManager nav)
        {
            connection = new HubConnectionBuilder()
                .WithUrl(nav.ToAbsoluteUri("/shortcuthub"))
                .WithAutomaticReconnect()
                .Build();

            connection.On<Shortcut>("ShortcutPressed", shortcut =>
            {
                if (shortcut == Shortcut.ToggleLock)
                    ToggleLockShortcutReceived?.Invoke();
            });
        }

        public async Task StartAsync() => await connection.StartAsync();
        public async Task StopAsync() => await connection.StopAsync();
    }
}