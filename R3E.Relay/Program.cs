using Microsoft.Extensions.Logging;
using R3E.Core.SharedMemory;
using R3E.Data;
using R3E.Relay.Service;
using R3E.Utilities;
using System.CommandLine;
using System.Net;
using System.Runtime.InteropServices;

internal class Program : IDisposable
{
    public SharedMemoryService sharedMemoryService;
    public UdpRelayService udpRelayService;
    private readonly ILoggerFactory? loggerFactory;
    private readonly ILogger<Program> logger;
    private bool disposed;

    // Reusable buffer for serialization to avoid repeated allocations
    private readonly byte[] sendBuffer;
    private readonly int bufferSize;

    public Program(string targetHost, int targetPort)
    {
        // keep logger factory alive for the lifetime of the program
        loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
        var shmLogger = loggerFactory.CreateLogger<SharedMemoryService>();
        var udpLogger = loggerFactory.CreateLogger<UdpRelayService>();
        logger = loggerFactory.CreateLogger<Program>();

        sharedMemoryService = new SharedMemoryService(shmLogger);
        int sourcePort = GetAvailablePort();
        udpRelayService = new UdpRelayService(sourcePort, targetHost, targetPort, udpLogger);

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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        // Unsubscribe from events before disposing
        sharedMemoryService.DataUpdated -= OnDataUpdated;

        try
        {
            sharedMemoryService.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing SharedMemoryService");
        }

        try
        {
            udpRelayService.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing UdpRelayService");
        }

        try
        {
            loggerFactory?.Dispose();
        }
        catch (Exception ex)
        {
            // Dispose of logger factory shouldn't throw, but guard anyway
            logger.LogError(ex, "Error disposing LoggerFactory");
        }

        GC.SuppressFinalize(this);
    }

    static async Task<int> Main(string[] args)
    {
        var udpPortOption = LaunchOptions.CreatePortOption(
            "--udp-port", "UDP port to send telemetry to. Must match the HUD's --udp-port.",
            RemoteSharedMemoryService.DefaultUdpPort);
        var udpHostOption = new Option<string>("--udp-host")
        {
            Description = "IP address to send telemetry to. Use the HUD machine's address when it is not on this machine.",
            DefaultValueFactory = _ => "127.0.0.1",
            HelpName = "ip",
        };
        udpHostOption.Validators.Add(result =>
        {
            // UdpRelayService parses this with IPAddress.Parse, which would throw on a hostname.
            var value = result.GetValueOrDefault<string>();
            if (!IPAddress.TryParse(value, out _))
            {
                result.AddError($"--udp-host must be an IP address (hostnames are not supported), but was '{value}'.");
            }
        });

        var rootCommand = new RootCommand("R3E relay - forwards RaceRoom shared memory to YaHud over UDP.");
        rootCommand.Options.Add(udpPortOption);
        rootCommand.Options.Add(udpHostOption);

        var parsed = rootCommand.Parse(args);
        if (parsed.Errors.Count > 0 || parsed.Action is not null)
        {
            // Invalid input, --help or --version: print the parser's own output and exit.
            return parsed.Invoke();
        }

        var targetHost = parsed.GetValue(udpHostOption)!;
        var targetPort = parsed.GetValue(udpPortOption);

        Console.WriteLine("Starting R3E API UDP relay service");
        Console.WriteLine($"Relaying R3E shared memory to {targetHost}:{targetPort}");
        Console.WriteLine("Waiting for R3E to start");
        using var program = new Program(targetHost, targetPort);

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

        return 0;
    }
}