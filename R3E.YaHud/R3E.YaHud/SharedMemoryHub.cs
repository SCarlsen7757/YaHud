using Microsoft.AspNetCore.SignalR;

namespace R3E.YaHud
{
    public class SharedMemoryHub : Hub
    {
        // Server can call Clients.All.SendAsync("UpdateShared", sharedData)
    }
}