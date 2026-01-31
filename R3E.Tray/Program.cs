using System.Runtime.InteropServices;

namespace R3E.Tray;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("Using Linux");
            Linux.LinuxTray.Run();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Using Windows");
            //Windows.WindowsTray.Run();
            return;
        }

        Console.WriteLine("Unsupported OS");
    }
}
