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

builder.Services.AddSingleton<TelemetryService>(sp => new(new SharedMemoryService(false))); // Set useUdp to true to use UDP shared memory on Windows

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
