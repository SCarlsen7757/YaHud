namespace R3E.Tray.Linux;

using Gtk;

class LinuxTray
{
    static void Main(string[] args)
    {
        Application.Init();

        var tray = new StatusIcon(new Gdk.Pixbuf("icon.png"))
        {
            Visible = true,
            TooltipText = "YaHud"
        };

        var menu = new Menu();

        var quit = new MenuItem("Quit");
        quit.Activated += (_, _) => Application.Quit();

        menu.Append(quit);
        menu.ShowAll();

        tray.PopupMenu += (_, _) => menu.Popup();

        Application.Run();
    }
}
