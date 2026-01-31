namespace R3E.Tray;
using Microsoft.AspNetCore.Builder; // <-- needed for WebApplication

public static class TrayProgram
{
    public static void Main(WebApplication? hostApp = null)
    {
    #if LINUX
        Console.WriteLine("Using Linux");
        Linux.LinuxTray.Run(hostApp);
    #endif
         
    #if WINDOWS
        Console.WriteLine("Using Windows");
        Windows.WindowsTray.Run(hostApp);
    #endif

    #if !LINUX && !WINDOWS
        Console.WriteLine("Unsupported OS");
    #endif
    }
}
