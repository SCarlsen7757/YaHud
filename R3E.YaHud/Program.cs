using Microsoft.AspNetCore.Components.Server;
using R3E.Core.Interfaces;
using R3E.Core.Services;
using R3E.Core.SharedMemory;
using R3E.Features.Driver;
using R3E.Features.Fuel;
using R3E.Features.Image;
using R3E.Features.Sector;
using R3E.Features.TimeGap;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
    options.SingleLine = true;
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

#if DEBUG
builder.Services.Configure<CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});
#endif

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IImageService, ImageService>();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<HudLockService>();
builder.Services.AddScoped<VisibilityService>();
builder.Services.AddScoped<TestModeService>();
builder.Services.AddSingleton<ShortcutService>();

// Register appropriate ISharedSource based on OS
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<SharedMemoryService>();
    builder.Services.AddSingleton<ISharedSource>(sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    });
    builder.Services.AddSingleton<IHostedService>(sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    });
}
else
{
    builder.Services.AddSingleton<RemoteSharedMemoryService>();
    builder.Services.AddSingleton<ISharedSource>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
}

// Core services
builder.Services.AddSingleton<ITelemetryEventBus, TelemetryEventBus>();
builder.Services.AddSingleton<ITelemetryService, TelemetryService>();

// Feature services - register independently
builder.Services.AddSingleton<ITimeGapService, SimpleTimeGapService>();
builder.Services.AddSingleton<FuelService>();
builder.Services.AddSingleton<DriverService>();
builder.Services.AddSingleton<SectorService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<R3E.YaHud.Components.App>()
    .AddInteractiveServerRenderMode()
    .WithStaticAssets();

app.Run();
