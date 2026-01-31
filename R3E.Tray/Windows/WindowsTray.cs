#if WINDOWS
namespace R3E.Tray.Windows;
using Microsoft.Extensions.Hosting;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;


public static class WindowsTray
{
    public static void Run(WebApplication? hostApp = null)
    {
        // Path to the tray icon in Assets folder
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "trayfavicon.png");

        if (!File.Exists(iconPath))
        {
            Console.WriteLine($"Tray icon not found: {iconPath}");
            return;
        }

        // Create the NotifyIcon (tray icon)
        using var tray = new NotifyIcon
        {
            Icon = new Icon(iconPath),
            Visible = true,
            Text = "YaHud"
        };

        // Create a context menu with a Quit item
        var menu = new ContextMenuStrip();
        var quitItem = new ToolStripMenuItem("Quit");
        
        
        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) =>
        {
            Console.WriteLine("Quit selected, stopping tray + server...");

            // Hide the tray icon
            tray.Visible = false;

            // Stop the Blazor host quickly
            hostApp?.StopAsync(TimeSpan.FromMilliseconds(500)).Wait();

            // Exit the tray thread
            Application.Exit();
        };

        menu.Items.Add(quitItem);
        tray.ContextMenuStrip = menu;

        // Run the Windows Forms message loop
        Application.Run();
    }
}
#endif