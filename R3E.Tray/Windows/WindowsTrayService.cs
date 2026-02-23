#if WINDOWS
namespace R3E.Tray.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Windows.Forms;

public class WindowsTrayService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<WindowsTrayService> logger;
    private Thread? winFormsThread;
    private bool disposed;

    public WindowsTrayService(IHostApplicationLifetime lifetime, ILogger<WindowsTrayService> logger)
    {
        this.lifetime = lifetime;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        winFormsThread = new Thread(RunWinFormsMessageLoop)
        {
            IsBackground = true,
            Name = "WinForms-Tray-Thread"
        };
        winFormsThread.SetApartmentState(ApartmentState.STA);
        winFormsThread.Start();

        logger.LogInformation("Windows tray service started.");
        return Task.CompletedTask;
    }

    private void RunWinFormsMessageLoop()
    {
        NotifyIcon? trayIcon = null;
        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

            var assembly = typeof(WindowsTrayService).Assembly;
            const string iconResourceName = "R3E.Tray.Assets.trayfavicon.png";

            using var iconStream = assembly.GetManifestResourceStream(iconResourceName);
            if (iconStream == null)
            {
                logger.LogWarning("Tray icon resource not found: {ResourceName}", iconResourceName);
                return;
            }

            using var bitmap = new Bitmap(iconStream);
            var icon = Icon.FromHandle(bitmap.GetHicon());

            trayIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "YaHud",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            var quitItem = contextMenu.Items.Add("Quit");
            quitItem.Click += (_, _) =>
            {
                lifetime.StopApplication();
            };
            trayIcon.ContextMenuStrip = contextMenu;

            // When the host signals stopping, exit the WinForms message loop
            lifetime.ApplicationStopping.Register(() =>
            {
                System.Windows.Forms.Application.Exit();
            });

            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Windows tray initialization failed.");
        }
        finally
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Windows tray service stopping.");
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
