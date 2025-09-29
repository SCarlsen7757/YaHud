using Microsoft.AspNetCore.SignalR;
using R3E.API;
using R3E.YaHud;
using R3E.YaHud.Client;
using R3E.YaHud.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<R3E.YaHud.Client.HudLockService>();

// Only add SharedMemoryService if running on Windows
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<SharedMemoryService>();
    builder.Services.AddScoped<SharedMemoryClientService>();
}

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Add this line before building the app to register SignalR services
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(R3E.YaHud.Client._Imports).Assembly);

app.MapHub<SharedMemoryHub>("/sharedmemoryhub");

// Wire up SharedMemoryService to push updates to SignalR clients
if (OperatingSystem.IsWindows())
{
    var sharedMemoryService = app.Services.GetRequiredService<SharedMemoryService>();
    var hubContext = app.Services.GetRequiredService<IHubContext<SharedMemoryHub>>();

    sharedMemoryService.DataUpdated += async (data) =>
    {
        await hubContext.Clients.All.SendAsync("UpdateShared", data);
    };
}

app.Run();
