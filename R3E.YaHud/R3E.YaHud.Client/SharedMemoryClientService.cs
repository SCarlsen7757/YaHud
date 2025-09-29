using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using R3E.Data;

namespace R3E.YaHud.Client
{
    public class SharedMemoryClientService
    {
        private readonly HubConnection connection;
        public Shared Data { get; private set; }
        public event Action<Shared>? DataUpdated;

        public SharedMemoryClientService(NavigationManager nav)
        {
            Data = new();
            connection = new HubConnectionBuilder()
                .WithUrl(nav.ToAbsoluteUri("/sharedmemoryhub"))
                .WithAutomaticReconnect()
                .Build();

            connection.On<Shared>("UpdateShared", data =>
            {
                Data = data;
                DataUpdated?.Invoke(data);
            });
        }

        public async Task StartAsync() => await connection.StartAsync();
        public async Task StopAsync() => await connection.StopAsync();
    }
}