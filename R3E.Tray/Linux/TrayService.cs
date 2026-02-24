#if LINUX
namespace R3E.Tray.Linux;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gtk;

public class TrayService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<TrayService> logger;
    private Thread? gtkThread;
    private bool disposed;

    public TrayService(IHostApplicationLifetime lifetime, ILogger<TrayService> logger)
    {
        this.lifetime = lifetime;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        gtkThread = new Thread(RunGtkMainLoop)
        {
            IsBackground = true,
            Name = "GTK-Tray-Thread"
        };
        gtkThread.Start();

        logger.LogInformation("Linux tray service started.");
        return Task.CompletedTask;
    }

    private void RunGtkMainLoop()
    {
        try
        {
            Application.Init();

            var assembly = typeof(TrayService).Assembly;
            const string iconResourceName = "R3E.Tray.Assets.trayfavicon.png";

            using var iconStream = assembly.GetManifestResourceStream(iconResourceName);
            if (iconStream == null)
            {
                logger.LogWarning("Tray icon resource not found: {ResourceName}", iconResourceName);
                return;
            }

            var trayPixbuf = new Gdk.Pixbuf(iconStream);
            var tray = new StatusIcon(trayPixbuf)
            {
                Visible = true,
                TooltipText = "YaHud"
            };

            var menu = new Menu();
            var quit = new MenuItem("Quit");

            quit.Activated += (_, _) =>
            {
                lifetime.StopApplication();
            };
            menu.Append(quit);
            menu.ShowAll();

            tray.PopupMenu += (_, _) => menu.Popup();

            // When the host signals stopping, quit the GTK loop from the GTK thread
            lifetime.ApplicationStopping.Register(() =>
            {
                Application.Invoke((_, _) => Application.Quit());
            });

            Application.Run();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Linux tray initialization failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Linux tray service stopping.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        GC.SuppressFinalize(this);
    }
}
#endif
