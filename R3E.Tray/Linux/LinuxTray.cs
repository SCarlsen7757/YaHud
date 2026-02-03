#if LINUX
namespace R3E.Tray.Linux;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using Gtk;
using Microsoft.AspNetCore.Builder; // <-- needed for WebApplication
public static class LinuxTray
{
    public static void Run(WebApplication? hostApp = null)
    {
        Application.Init();

        // Load tray icon from embedded resource to avoid relying on app base directory
        var assembly = typeof(LinuxTray).Assembly;
        const string iconResourceName = "R3E.Tray.Assets.trayfavicon.png";

        using var iconStream = assembly.GetManifestResourceStream(iconResourceName);
        if (iconStream == null)
        {
            Console.WriteLine($"Tray icon resource not found: {iconResourceName}");
            return; // stop if icon missing
        }

        var trayPixbuf = new Gdk.Pixbuf(iconStream);
        var tray = new StatusIcon(trayPixbuf)
        {
            Visible = true,
            TooltipText = "YaHud"
        };

        // Create a simple menu
        var menu = new Menu();
        var quit = new MenuItem("Quit");
        
        quit.Activated += (_, _) =>
        {
            // Stop Blazor host
            hostApp?.StopAsync(TimeSpan.FromMilliseconds(500)).Wait();
            Application.Quit(); // GTK main loop exits
        };
        menu.Append(quit);
        menu.ShowAll();
        
        // Correct PopupMenu handler
        tray.PopupMenu += (_, _) => menu.Popup(); // simple version that works
        Application.Run();
    }
}
#endif