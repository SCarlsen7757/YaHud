using System.Diagnostics;

namespace R3E.Utilities
{
    public static class Game
    {
        public static bool IsRrreRunning()
        {
            return Process.GetProcessesByName("RRRE").Length > 0 || Process.GetProcessesByName("RRRE64").Length > 0;
        }
    }
}
