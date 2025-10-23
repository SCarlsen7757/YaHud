using Microsoft.Extensions.Logging;
using R3E.API;
using R3E.Data;
using R3E.UdpRelay;
using System.Net;
using System.Runtime.InteropServices;

internal class Program : IAsyncDisposable
{
    public SharedMemoryService sharedMemoryService;
    public UdpRelayService udpRelayService;
    private readonly ILoggerFactory? loggerFactory;
    private readonly ILogger<Program> logger;
    private bool disposed;

    // Reusable buffer for serialization to avoid repeated allocations
    private readonly byte[] sendBuffer;
    private readonly int bufferSize;

    public Program()
    {
        // keep logger factory alive for the lifetime of the program
        loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
        var shmLogger = loggerFactory.CreateLogger<SharedMemoryService>();
        var udpLogger = loggerFactory.CreateLogger<UdpRelayService>();
        logger = loggerFactory.CreateLogger<Program>();

        sharedMemoryService = new SharedMemoryService(shmLogger);
        int sourcePort = GetAvailablePort();
        udpRelayService = new UdpRelayService(sourcePort, "127.0.0.1", 10101, udpLogger);

        // Allocate buffer once for the lifetime of the program
        bufferSize = Marshal.SizeOf<Shared>();
        sendBuffer = new byte[bufferSize];

        sharedMemoryService.DataUpdated += OnDataUpdated;
    }

    private void OnDataUpdated(Shared data)
    {
        try
        {
            // Serialize Shared struct to byte array using the reusable buffer
            var handle = GCHandle.Alloc(sendBuffer, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            // Send via UDP relay with proper async handling
            // Use fire-and-forget pattern but with proper error handling
            _ = SendDataAsync(sendBuffer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing data update");
        }
    }

    private async Task SendDataAsync(byte[] buffer)
    {
        try
        {
            await udpRelayService.SendAsync(buffer).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending UDP data");
        }
    }

    private static int GetAvailablePort()
    {
        using var udp = new System.Net.Sockets.UdpClient(0);
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        // Unsubscribe from events before disposing
        sharedMemoryService.DataUpdated -= OnDataUpdated;

        // Dispose services asynchronously
        await sharedMemoryService.DisposeAsync().ConfigureAwait(false);
        udpRelayService.Dispose();
        loggerFactory?.Dispose();

        GC.SuppressFinalize(this);
    }

    static async Task Main()
    {
        Console.WriteLine("Starting R3E API UDP relay service");
        Console.WriteLine("Waiting for R3E to start");
        await using var program = new Program();

        // Use a CancellationTokenSource to allow graceful shutdown
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Start the background service
        await program.sharedMemoryService.StartAsync(cts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Shutting down...");
        }
        finally
        {
            // Stop the background service gracefully
            await program.sharedMemoryService.StopAsync(CancellationToken.None);
        }
    }
}