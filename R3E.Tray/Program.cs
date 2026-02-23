namespace R3E.Tray;

using Microsoft.Extensions.DependencyInjection;

public static class TrayServiceExtensions
{
    public static IServiceCollection AddTrayService(this IServiceCollection services)
    {
#if WINDOWS
        services.AddHostedService<Windows.WindowsTrayService>();
#elif LINUX
        services.AddHostedService<Linux.LinuxTrayService>();
#endif
        return services;
    }
}
