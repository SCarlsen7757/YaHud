using R3E.API;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<HudLockService>();
builder.Services.AddSingleton<ShortcutService>();

// Register appropriate ISharedSource based on OS
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<ISharedSource, SharedMemoryService>();
    builder.Services.AddHostedService(sp => (SharedMemoryService)sp.GetRequiredService<ISharedSource>());
}
else
{
    builder.Services.AddSingleton<ISharedSource, RemoteSharedMemoryService>();
    builder.Services.AddHostedService(sp => (RemoteSharedMemoryService)sp.GetRequiredService<ISharedSource>());
}

// TelemetryService depends on ISharedSource. Let DI construct it so ILogger is injected.
builder.Services.AddSingleton<TelemetryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<R3E.YaHud.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
