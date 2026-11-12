using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Connections;
using R3E.Core.Interfaces;
using R3E.Core.Recording;
using R3E.Core.Replay;
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

// Recording is opt-in so nobody writes multi-GB files by accident. --record makes the feature
// available but inert until started from the UI; --record-autostart is for scripted or headless
// capture where there is nobody to press the button, and therefore implies --record.
var recordOption = new Option<bool>("--record")
{
    Description = "Make telemetry recording available. Registers the recorder and shows the record controls; recording stays inert until started.",
};
var recordAutoStartOption = new Option<bool>("--record-autostart")
{
    Description = "Begin recording at launch without pressing the button. Implies --record.",
};
var recordingDirOption = new Option<string>("--recording-dir")
{
    Description = "Folder recordings are written to. Defaults to <LocalApplicationData>/YaHud/recordings.",
    HelpName = "dir",
};
var recordingBlockSecondsOption = LaunchOptions.CreateIntOption(
    "--recording-block-seconds",
    "Compression block duration in seconds. Smaller blocks seek more finely but compress worse.",
    RecordingOptions.DefaultBlockSeconds,
    RecordingOptions.MinBlockSeconds,
    RecordingOptions.MaxBlockSeconds,
    "seconds");
var replayOption = new Option<string>("--replay")
{
    Description = "Start in replay mode on this recording file instead of reading live telemetry.",
    HelpName = "file",
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
rootCommand.Options.Add(recordOption);
rootCommand.Options.Add(recordAutoStartOption);
rootCommand.Options.Add(recordingDirOption);
rootCommand.Options.Add(recordingBlockSecondsOption);
rootCommand.Options.Add(replayOption);

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
if (parsed.GetResult(recordOption) is { Implicit: false })
{
    overrides["YaHud:Recording:Enabled"] = parsed.GetValue(recordOption).ToString();
}
if (parsed.GetResult(recordAutoStartOption) is { Implicit: false })
{
    overrides["YaHud:Recording:AutoStart"] = parsed.GetValue(recordAutoStartOption).ToString();
}
if (parsed.GetResult(recordingDirOption) is { Implicit: false })
{
    overrides["YaHud:Recording:Directory"] = parsed.GetValue(recordingDirOption);
}
if (parsed.GetResult(recordingBlockSecondsOption) is { Implicit: false })
{
    overrides["YaHud:Recording:BlockSeconds"] = parsed.GetValue(recordingBlockSecondsOption).ToString();
}
if (parsed.GetResult(replayOption) is { Implicit: false })
{
    overrides["YaHud:Replay:File"] = parsed.GetValue(replayOption);
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

if (!LaunchOptions.TryReadBool(builder.Configuration["YaHud:Udp:ForceUdp"], "--force-udp (or YaHud:Udp:ForceUdp)", false, out var forceUdp, out configError)
    || !LaunchOptions.TryReadBool(builder.Configuration["YaHud:Recording:Enabled"], "--record (or YaHud:Recording:Enabled)", false, out var recordEnabled, out configError)
    || !LaunchOptions.TryReadBool(builder.Configuration["YaHud:Recording:AutoStart"], "--record-autostart (or YaHud:Recording:AutoStart)", false, out var recordAutoStart, out configError)
    || !LaunchOptions.TryReadInt(builder.Configuration["YaHud:Recording:BlockSeconds"], "--recording-block-seconds (or YaHud:Recording:BlockSeconds)", RecordingOptions.DefaultBlockSeconds, RecordingOptions.MinBlockSeconds, RecordingOptions.MaxBlockSeconds, out var blockSeconds, out configError))
{
    Console.Error.WriteLine(configError);
    return 1;
}

// AutoStart is only meaningful if the feature is available at all, so it implies Enabled - a user
// who asked to start recording immediately plainly wants recording.
var recordingOptions = new RecordingOptions
{
    Enabled = recordEnabled || recordAutoStart,
    AutoStart = recordAutoStart,
    Directory = builder.Configuration["YaHud:Recording:Directory"],
    BlockSeconds = blockSeconds,
};

// Fail here rather than at the first frame: by then the user is driving, the HUD looks fine, and
// the only symptom is that no file ever appears.
if (recordingOptions.Enabled)
{
    try
    {
        Directory.CreateDirectory(recordingOptions.ResolveDirectory());
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        Console.Error.WriteLine($"--recording-dir (or YaHud:Recording:Directory) could not be created: '{recordingOptions.ResolveDirectory()}'. {ex.Message}");
        return 1;
    }
}

// Likewise for replay: a mistyped path should say so now, not surface as an empty HUD.
var replayFile = builder.Configuration["YaHud:Replay:File"];
if (!string.IsNullOrWhiteSpace(replayFile))
{
    try
    {
        replayFile = Path.GetFullPath(replayFile);
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
    {
        Console.Error.WriteLine($"--replay (or YaHud:Replay:File) is not a usable path: '{replayFile}'. {ex.Message}");
        return 1;
    }

    if (!File.Exists(replayFile))
    {
        Console.Error.WriteLine($"--replay (or YaHud:Replay:File) must point at an existing recording, but '{replayFile}' does not exist.");
        return 1;
    }
}
else
{
    replayFile = null;
}

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

// Register the appropriate live telemetry source based on OS, unless --force-udp overrides it.
// Note what is deliberately absent: neither is registered as ISharedSource any more. That
// registration now belongs to SharedSourceSwitch, so replay can take over at runtime without a
// restart. Each live source is still registered as itself (so the switch can be handed the
// concrete instance) and as IHostedService (so the host starts and stops it as before).
Func<IServiceProvider, ISharedSource> resolveLiveSource;
if (OperatingSystem.IsWindows() && !forceUdp)
{
    builder.Services.AddSingleton<SharedMemoryService>();
    builder.Services.AddSingleton<IHostedService>(sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    });
    resolveLiveSource = sp =>
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return sp.GetRequiredService<SharedMemoryService>();
#pragma warning restore CA1416 // Validate platform compatibility
    };
}
else
{
    builder.Services.AddSingleton(sp => new RemoteSharedMemoryService(udpPort, sp.GetRequiredService<ILoggerFactory>()));
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RemoteSharedMemoryService>());
    resolveLiveSource = sp => sp.GetRequiredService<RemoteSharedMemoryService>();
}

// Replay is a third ISharedSource, in-process: no UDP hop and no second application. The switch
// forwards whichever of the two is active, so ITelemetryService subscribes once at construction
// and never notices a swap.
builder.Services.AddSingleton<FileSharedSource>();
builder.Services.AddSingleton(sp => new SharedSourceSwitch(
    resolveLiveSource(sp),
    sp.GetRequiredService<FileSharedSource>(),
    sp.GetRequiredService<ILogger<SharedSourceSwitch>>()));
builder.Services.AddSingleton<ISharedSource>(sp => sp.GetRequiredService<SharedSourceSwitch>());
builder.Services.AddSingleton<ReplayController>();

// Recording options are always registered so the UI can read them and explain why the controls are
// hidden; the recorder itself is only registered when the feature is enabled, so a default launch
// carries none of its cost. It stays inert until started unless AutoStart is set.
builder.Services.AddSingleton(recordingOptions);
if (recordingOptions.Enabled)
{
    builder.Services.AddSingleton<TelemetryRecorder>();
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TelemetryRecorder>());
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
if (recordingOptions.Enabled)
{
    app.Logger.LogInformation("Telemetry recording is enabled: writing {BlockSeconds}s blocks to {Directory}.", recordingOptions.BlockSeconds, recordingOptions.ResolveDirectory());
    if (recordingOptions.AutoStart)
    {
        app.Logger.LogInformation("--record-autostart is set: recording begins as soon as the first telemetry frame arrives.");
    }
}

// Resolve the switch eagerly. It subscribes to the live source in its constructor, and every other
// consumer reaches it lazily through ISharedSource - the first Blazor circuit, or the recorder.
// Creating it here makes that subscription happen at a known point instead of whenever the first
// browser connects, and turns a misconfiguration into a startup failure rather than a blank HUD.
_ = app.Services.GetRequiredService<SharedSourceSwitch>();

if (replayFile is not null)
{
    // --replay pre-selects replay mode, so a recording can be opened without touching the UI.
    try
    {
        app.Services.GetRequiredService<ReplayController>().Load(replayFile);
        app.Logger.LogInformation("--replay is set: starting in replay mode on {File}. Live telemetry is not being read.", replayFile);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not open the recording '{File}'.", replayFile);
        return 1;
    }
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
