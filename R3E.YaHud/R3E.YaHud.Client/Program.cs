using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R3E.YaHud.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<HudLockService>();

await builder.Build().RunAsync();
