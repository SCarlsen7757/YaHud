using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R3E.YaHud.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<SharedMemoryClientService>();
builder.Services.AddSingleton<ShortcutClientService>();
builder.Services.AddScoped<HudLockService>();

await builder.Build().RunAsync();
