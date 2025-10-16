using R3E.API;
using R3E.Data;
using R3E.UdpRelay;
using System.Net;
using System.Runtime.InteropServices;

internal class Program : IDisposable
{
    public SharedMemoryService sharedMemoryService;
    public UdpRelayService udpRelayService;

    public Program()
    {
        sharedMemoryService = new SharedMemoryService(false);
        int sourcePort = GetAvailablePort();
        udpRelayService = new UdpRelayService(sourcePort, "127.0.0.1", 10101);
        sharedMemoryService.DataUpdated += OnDataUpdated;
    }

    private void OnDataUpdated(Shared data)
    {
        try
        {
            // Serialize Shared struct to JSON
            var size = Marshal.SizeOf<Shared>();
            var buffer = new byte[size];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            handle.Free();

            // Send via UDP relay
            udpRelayService.SendAsync(buffer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[R3E Error] {ex.Message}");
        }
    }

    private static int GetAvailablePort()
    {
        using var udp = new System.Net.Sockets.UdpClient(0);
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    public void Dispose()
    {
        sharedMemoryService.Dispose();
        udpRelayService.Dispose();
        GC.SuppressFinalize(this);
    }

    static async Task Main()
    {
        Console.WriteLine("Starting R3E API UDP relay service");
        Console.WriteLine("Waiting for R3E to start");
        using var program = new Program();

        while (true)
        {
            await Task.Delay(1000); // keeps app alive
        }
    }

}