#if LINUX
namespace R3E.Tray.Linux;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

public class TrayService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<TrayService> logger;
    private DBusConnection? connection;
    private bool disposed;

    public TrayService(IHostApplicationLifetime lifetime, ILogger<TrayService> logger)
    {
        this.lifetime = lifetime;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (iconWidth, iconHeight, iconArgbData) = LoadIconPixelData();
            var sniHandler = new StatusNotifierItemHandler(iconWidth, iconHeight, iconArgbData);
            var menuHandler = new DbusTrayMenuHandler(() => lifetime.StopApplication());

            connection = new DBusConnection(DBusAddress.Session!);
            await connection.ConnectAsync().ConfigureAwait(false);

            connection.AddMethodHandler(sniHandler);
            connection.AddMethodHandler(menuHandler);

            await RegisterWithWatcher().ConfigureAwait(false);

            logger.LogInformation("Linux tray service started via D-Bus StatusNotifierItem.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Linux tray initialization failed. The system tray icon will not be available.");
        }
    }

    private (int Width, int Height, byte[] ArgbData) LoadIconPixelData()
    {
        var assembly = typeof(TrayService).Assembly;
        const string iconResourceName = "R3E.Tray.Assets.trayfavicon.png";

        using var iconStream = assembly.GetManifestResourceStream(iconResourceName)
            ?? throw new FileNotFoundException($"Tray icon resource not found: {iconResourceName}");

        return PngPixelReader.ReadPng(iconStream);
    }

    private async Task RegisterWithWatcher()
    {
        // MessageWriter is a ref struct: build the message and dispose the writer
        // before awaiting the call.
        MessageBuffer message = CreateRegisterMessage();

        await connection!.CallMethodAsync(message).ConfigureAwait(false);
        logger.LogInformation("Registered with StatusNotifierWatcher as {BusName}.", connection.UniqueName);
    }

    private MessageBuffer CreateRegisterMessage()
    {
        using var writer = connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: "org.kde.StatusNotifierWatcher",
            path: "/StatusNotifierWatcher",
            @interface: "org.kde.StatusNotifierWatcher",
            member: "RegisterStatusNotifierItem",
            signature: "s",
            flags: MessageFlags.None);
        writer.WriteString(connection.UniqueName!);
        return writer.CreateMessage();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Linux tray service stopping.");
        connection?.Dispose();
        connection = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        connection?.Dispose();
        connection = null;
        GC.SuppressFinalize(this);
    }
}
#endif
