using Microsoft.AspNetCore.SignalR;
using R3E.API;
using R3E.Data;
using R3E.YaHud.Client.Services;
using R3E.YaHud.Client.Services.Settings;
using R3E.YaHud.Components;
using R3E.YaHud.Hubs;
using R3E.YaHud.Services;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
       .AddMessagePackProtocol();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<HudLockService>();
builder.Services.AddSingleton<ShortcutService>();
builder.Services.AddScoped<ShortcutClientService>();

builder.Services.AddSingleton<SharedMemoryService>(sp => new(false)); // Set useUdp to true to use UDP shared memory on Windows
builder.Services.AddScoped<SharedMemoryClientService>();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

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
app.MapHub<ShortcutHub>("/shortcuthub");

var sharedMemoryService = app.Services.GetRequiredService<SharedMemoryService>();
var hubContext = app.Services.GetRequiredService<IHubContext<SharedMemoryHub>>();

sharedMemoryService.DataUpdated += async (data) =>
{
    var size = Marshal.SizeOf<Shared>();
    var buffer = new byte[size];
    var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
    handle.Free();

    await hubContext.Clients.All.SendAsync("UpdateSharedBinary", buffer);
};

var shortcutService = app.Services.GetRequiredService<ShortcutService>();
var shortcutHub = app.Services.GetRequiredService<IHubContext<ShortcutHub>>();

shortcutService.ShortcutPressed += async (shortcut) =>
{
    await shortcutHub.Clients.All.SendAsync("ShortcutPressed", shortcut);
};

app.Run();
