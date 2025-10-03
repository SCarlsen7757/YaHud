using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R3E.YaHud.Client.Services;
using R3E.YaHud.Client.Services.Settings;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<SettingsService>();
builder.Services.AddSingleton<SharedMemoryClientService>();
builder.Services.AddSingleton<ShortcutClientService>();
builder.Services.AddScoped<HudLockService>();

await builder.Build().RunAsync();
