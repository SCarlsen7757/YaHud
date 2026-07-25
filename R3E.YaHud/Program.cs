using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Connections;
using R3E.Core.Interfaces;
using R3E.Core.Services;
using R3E.Core.SharedMemory;
using R3E.Features.Driver;
using R3E.Features.Fuel;
using R3E.Features.Image;
using R3E.Features.Radar;
using R3E.Features.Sector;
using R3E.Features.TimeGap;
using R3E.Tray;
using R3E.Features.TireWidget;
using R3E.YaHud.Services;
using R3E.YaHud.Services.Settings;
using R3E.Utilities;
using System.CommandLine;

var webPortOption = LaunchOptions.CreatePortOption(
    "--web-port", "Port the web HUD listens on.", 5000);
var udpPortOption = LaunchOptions.CreatePortOption(
    "--udp-port", "UDP port the telemetry receiver listens on. Must match the relay's --udp-port.",
    RemoteSharedMemoryService.DefaultUdpPort);
var forceUdpOption = new Option<bool>("--force-udp")
{
    Description = "Receive telemetry over UDP from the relay instead of reading shared memory directly (Windows only; it is already the default elsewhere).",
};

var rootCommand = new RootCommand("YaHud - a web HUD for RaceRoom Racing Experience.")
{
    // Host arguments such as --urls, --environment and --contentRoot must keep working, so
    // anything we do not recognise is forwarded to the web host rather than rejected here.
    TreatUnmatchedTokensAsErrors = false,
};
rootCommand.Options.Add(webPortOption);
rootCommand.Options.Add(udpPortOption);
rootCommand.Options.Add(forceUdpOption);

var parsed = rootCommand.Parse(args);
if (parsed.Errors.Count > 0 || parsed.Action is not null)
{
    // Invalid input, --help or --version: print the parser's own output and exit.
    return parsed.Invoke();
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    // Only the arguments we did not claim; a valueless flag like --force-udp would otherwise
    // make the host's own command line configuration provider throw.
    Args = [.. parsed.UnmatchedTokens],
});

// Launch arguments win over appsettings.json, but only where the user actually supplied one -
// otherwise the file keeps providing the defaults.
var overrides = new Dictionary<string, string?>();
if (parsed.GetResult(webPortOption) is { Implicit: false })
{
    overrides["YaHud:WebPort"] = parsed.GetValue(webPortOption).ToString();
}
if (parsed.GetResult(udpPortOption) is { Implicit: false })
{
    overrides["YaHud:Udp:Port"] = parsed.GetValue(udpPortOption).ToString();
}
if (parsed.GetResult(forceUdpOption) is { Implicit: false })
{
    overrides["YaHud:Udp:ForceUdp"] = parsed.GetValue(forceUdpOption).ToString();
}
builder.Configuration.AddInMemoryCollection(overrides);

// Read through the same validation as the launch arguments: these values may also come from
// appsettings.json, where a typo would otherwise surface as an unhandled conversion exception.
if (!LaunchOptions.TryReadPort(builder.Configuration["YaHud:WebPort"], "--web-port (or YaHud:WebPort)", 5000, out var webPort, out var configError)
    || !LaunchOptions.TryReadPort(builder.Configuration["YaHud:Udp:Port"], "--udp-port (or YaHud:Udp:Port)", RemoteSharedMemoryService.DefaultUdpPort, out var udpPort, out configError))
{
    Console.Error.WriteLine(configError);
    return 1;
}

var rawForceUdp = builder.Configuration["YaHud:Udp:ForceUdp"];
if (!string.IsNullOrWhiteSpace(rawForceUdp) && !bool.TryParse(rawForceUdp, out _))
{
    Console.Error.WriteLine($"--force-udp (or YaHud:Udp:ForceUdp) must be true or false, but was '{rawForceUdp}'.");
    return 1;
}
var forceUdp = bool.TryParse(rawForceUdp, out var parsedForceUdp) && parsedForceUdp;

// UseUrls overrides --urls and ASPNETCORE_URLS, so only bind explicitly when the user asked for a
// port or nothing else has set the URLs.
var hudUrl = $"http://localhost:{webPort}";
var bindHudUrl = !string.IsNullOrEmpty(overrides.GetValueOrDefault("YaHud:WebPort"))
    || string.IsNullOrEmpty(builder.Configuration["urls"]);
if (bindHudUrl)
{
    builder.WebHost.UseUrls(hudUrl);
}

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
    options.SingleLine = true;
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

#if DEBUG
builder.Services.Configure<CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});
#endif

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IImageService, ImageService>();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<HudLockService>();
builder.Services.AddScoped<VisibilityService>();
builder.Services.AddScoped<TestModeService>();
builder.Services.AddSingleton<ShortcutService>();

// Register appropriate ISharedSource based on OS, unless --force-udp overrides it
if (OperatingSystem.IsWindows() && !forceUdp)
{
    builder.Services.AddSingleton<SharedMemoryService>();
    builder.Services.AddSingleton<ISharedSource>(sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    });
    builder.Services.AddSingleton<IHostedService>(sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    });
}
else
{
    builder.Services.AddSingleton(sp => new RemoteSharedMemoryService(udpPort, sp.GetRequiredService<ILoggerFactory>()));
    builder.Services.AddSingleton<ISharedSource>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
}

// Core services
builder.Services.AddSingleton<ITelemetryEventBus, TelemetryEventBus>();
builder.Services.AddSingleton<ITelemetryService, TelemetryService>();

// Feature services - register independently
builder.Services.AddSingleton<ITimeGapService, SimpleTimeGapService>();
builder.Services.AddSingleton<FuelService>();
builder.Services.AddSingleton<DriverService>();
builder.Services.AddSingleton<SectorService>();
builder.Services.AddSingleton<RadarService>();
builder.Services.AddSingleton<TireWidgetService>();

// System tray service
builder.Services.AddTrayService();

var app = builder.Build();

if (bindHudUrl)
{
    app.Logger.LogInformation("YaHud web HUD listening on {Url}", hudUrl);
}
if (forceUdp && OperatingSystem.IsWindows())
{
    app.Logger.LogWarning("--force-udp is set: receiving telemetry over UDP on port {Port} instead of reading shared memory. The relay must be running.", udpPort);
}

// Last-resort safety net: a fault in a background task (e.g. a widget update or a telemetry
// callback) must never tear down the process, since the Blazor UI and the UDP telemetry
// receiver are co-hosted here. Log and swallow instead of crashing.
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    app.Logger.LogError(e.Exception, "Unobserved task exception");
    e.SetObserved();
};
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    app.Logger.LogError(e.ExceptionObject as Exception, "Unhandled AppDomain exception (terminating: {IsTerminating})", e.IsTerminating);
};

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<R3E.YaHud.Components.App>()
    .AddInteractiveServerRenderMode()
    .WithStaticAssets();

try
{
    app.Run();
}
catch (IOException ex) when (ex.InnerException is AddressInUseException)
{
    // Picking a port is now a supported thing to do, so a clash deserves an explanation rather
    // than a stack trace.
    app.Logger.LogError("Port {Port} is already in use. Another program - possibly a second copy of YaHud - is listening on it. Start YaHud with --web-port=<port> to use a different one.", webPort);
    return 1;
}

return 0;
