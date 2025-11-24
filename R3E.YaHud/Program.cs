using Microsoft.AspNetCore.Components.Server;
using R3E.API;
using R3E.API.Image;
using R3E.API.TimeGap;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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
    builder.Services.AddSingleton<ISharedSource>(sp => sp.GetRequiredService<SharedMemoryService>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SharedMemoryService>());
}
else
{
    builder.Services.AddSingleton<RemoteSharedMemoryService>();
    builder.Services.AddSingleton<ISharedSource>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
}

builder.Services.AddSingleton<TimeGapService>();

// TelemetryService depends on ISharedSource. Let DI construct it so ILogger is injected.
builder.Services.AddSingleton<TelemetryService>();


builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
    options.SingleLine = true;
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});

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
