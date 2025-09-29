using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R3E.YaHud.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<HudLockService>();
builder.Services.AddSingleton<SharedMemoryClientService>();

await builder.Build().RunAsync();
