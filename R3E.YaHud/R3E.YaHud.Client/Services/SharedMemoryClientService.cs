using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using R3E.Data;
using System.Runtime.InteropServices;

namespace R3E.YaHud.Client.Services
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
       .AddMessagePackProtocol()
       .WithAutomaticReconnect()
       .Build();

            connection.On<byte[]>("UpdateSharedBinary", buffer =>
            {
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                var data = Marshal.PtrToStructure<Shared>(handle.AddrOfPinnedObject());
                handle.Free();

                Data = data;
                DataUpdated?.Invoke(data);
            });
        }

        public async Task StartAsync() => await connection.StartAsync();
        public async Task StopAsync() => await connection.StopAsync();
    }
}