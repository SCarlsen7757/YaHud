namespace R3E.Tray;

using Microsoft.Extensions.DependencyInjection;

public static class TrayServiceExtensions
{
    public static IServiceCollection AddTrayService(this IServiceCollection services)
    {
#if WINDOWS
        services.AddHostedService<Windows.TrayService>();
#elif LINUX
        services.AddHostedService<Linux.TrayService>();
#endif
        return services;
    }
}
