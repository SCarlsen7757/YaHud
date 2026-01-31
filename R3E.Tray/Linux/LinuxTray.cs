#if LINUX
namespace R3E.Tray.Linux;

using System;
using System.IO;
using Gtk;

public static class LinuxTray
{
    public static void Run()
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
        quit.Activated += (_, _) => Application.Quit();

        menu.Append(quit);
        menu.ShowAll();

        tray.PopupMenu += (_, _) =>
        {
            menu.Popup();
        };

        Application.Run();
    }
}
#endif