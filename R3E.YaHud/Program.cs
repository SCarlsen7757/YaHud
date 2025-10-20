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

// Register SharedMemoryService so it can be injected and also run as a hosted background service.
builder.Services.AddSingleton<SharedMemoryService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SharedMemoryService>());

// TelemetryService depends on SharedMemoryService. Let DI construct it so ILogger is injected.
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
