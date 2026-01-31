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

        // Path to the tray icon
        var iconPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "trayfavicon.png"
        );

        if (!File.Exists(iconPath))
        {
            Console.WriteLine($"Tray icon not found: {iconPath}");
            return; // stop if icon missing
        }

        var tray = new StatusIcon(new Gdk.Pixbuf(iconPath))
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