using System.Runtime.InteropServices;

namespace R3E.Tray;

internal static class Program
{
    public static void Main()
    {
    #if LINUX
        Console.WriteLine("Using Linux");
        Linux.LinuxTray.Run();
    #endif
         
    #if WINDOWS
        Console.WriteLine("Using Windows");
        Windows.WindowsTray.Run();
    #endif

    #if !LINUX && !WINDOWS
        Console.WriteLine("Unsupported OS");
    #endif
    }
}
