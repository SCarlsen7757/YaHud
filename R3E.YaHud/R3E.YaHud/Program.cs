using R3E.API;
using R3E.YaHud.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<R3E.YaHud.Client.HudLockService>();

// Only add SharedMemoryService if running on Windows
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<SharedMemoryService>();
}

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

app.Run();
