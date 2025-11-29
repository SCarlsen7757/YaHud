using Microsoft.Extensions.Logging.Abstractions;
using R3E.Data;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace R3E.API
{
    [SupportedOSPlatform("windows")]
    public class SharedMemoryService : BackgroundService, ISharedSource, IAsyncDisposable
    {
        private MemoryMappedFile? file;
        private Shared data;

        // Track last observed header values for change detection
        private int lastSimTicks;
        private bool fastPollingActive;
        private readonly ILogger<SharedMemoryService> logger;

        private readonly TimeSpan normalInterval = TimeSpan.FromMilliseconds(16); // ~60Hz
        private readonly TimeSpan notRunningInterval = TimeSpan.FromMilliseconds(5000); // Game not running update interval
        private readonly TimeSpan fastInterval = TimeSpan.FromMilliseconds(2.5); // ~400Hz

        // Start light count tracking
        private int lastStartLights = -1;

        // Offsets computed at runtime so code remains correct if SHM layout moves
        private static readonly int offsetPlayer;
        private static readonly int offsetGameSimulationTicks;
        private static readonly int offsetStartLights;
        private static readonly int offsetSessionPhase;

        // Reusable buffer for reading from the memory mapped file to avoid per-frame allocations
        private byte[]? readBuffer;
        private byte[]? fastReadBuffer;
        private bool disposed;

        static SharedMemoryService()
        {
            // Use Marshal.OffsetOf to compute offsets relative to the Shared struct
            offsetPlayer = (int)Marshal.OffsetOf<Shared>(nameof(Shared.Player));

            // Offset of GameSimulationTicks inside PlayerData
            offsetGameSimulationTicks = offsetPlayer + (int)Marshal.OffsetOf<PlayerData>(nameof(PlayerData.GameSimulationTicks));

            offsetStartLights = (int)Marshal.OffsetOf<Shared>(nameof(Shared.StartLights));
            offsetSessionPhase = (int)Marshal.OffsetOf<Shared>(nameof(Shared.SessionPhase));
        }

        public event Action<Shared>? DataUpdated;

        public event Action<int>? StartLightsChanged;

        public Shared Data => data;

        public SharedMemoryService(ILogger<SharedMemoryService>? logger = null)
        {
            this.logger = logger ?? NullLogger<SharedMemoryService>.Instance;
            data = new();
            this.logger.LogDebug("SharedMemoryService constructed");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var expected = Marshal.SizeOf<Shared>();

            // allocate read buffers once
            readBuffer = new byte[expected];
            fastReadBuffer = new byte[8]; // Only need 8 bytes for SessionPhase (4) + StartLights (4)

            logger.LogInformation("Starting shared memory poll loop (expected {Size} bytes)", expected);

            // Run both polling tasks concurrently
            var normalTask = NormalPollingLoopAsync(stoppingToken);
            var fastTask = FastPollingLoopAsync(stoppingToken);

            await Task.WhenAll(normalTask, fastTask).ConfigureAwait(false);
        }

        private async Task NormalPollingLoopAsync(CancellationToken stoppingToken)
        {
            var currentDelay = normalInterval;
            ulong noUpdate = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        logger.LogInformation("Opened shared memory '{Name}'", Constant.SharedMemoryName);
                        currentDelay = normalInterval;
                    }
                    catch (FileNotFoundException)
                    {
                        file = null;
                    }
                }

                if (file != null)
                {
                    try
                    {
                        using var view = file.CreateViewStream();

                        // Read into the reusable buffer to avoid allocations
                        var bytesRead = 0;
                        var expected = readBuffer!.Length;
                        while (bytesRead < expected)
                        {
                            var read = view.Read(readBuffer, bytesRead, expected - bytesRead);
                            if (read <= 0) break; // incomplete read
                            bytesRead += read;
                        }

                        if (bytesRead != expected)
                        {
                            logger.LogCritical("Incomplete shared memory read: {BytesRead}/{Expected}", bytesRead, expected);
                            await Task.Delay(currentDelay, stoppingToken).ConfigureAwait(false);
                            continue;
                        }

                        // Determine sim ticks directly from buffer using computed offsets
                        int simTicks = 0;
                        if (readBuffer.Length >= offsetGameSimulationTicks + 4)
                        {
                            simTicks = BitConverter.ToInt32(readBuffer, offsetGameSimulationTicks);
                        }

                        currentDelay = normalInterval;

                        // If nothing changed, skip
                        if (lastSimTicks == simTicks)
                        {
                            noUpdate++;
                            if (noUpdate > 327)
                            {
                                logger.LogInformation("No shared memory updates detected for a while (simTicks={SimTicks})", simTicks);
                                noUpdate = 0;
                            }
                        }
                        else
                        {
                            noUpdate = 0;
                            lastSimTicks = simTicks;

                            if (SharedMarshaller.TryMarshalShared(readBuffer, out var newData))
                            {
                                data = newData;

                                // Determine if fast polling should be active
                                UpdateFastPollingState(newData);

                                DataUpdated?.Invoke(data);
                            }
                            else
                            {
                                logger.LogCritical("Failed to marshal shared memory buffer");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error reading shared memory, disposing file handle");
                        file?.Dispose();
                        file = null;
                        currentDelay = notRunningInterval;
                    }
                }
                else
                {
                    // game not running - back off to reduce CPU
                    currentDelay = notRunningInterval;
                    fastPollingActive = false;
                }

                try
                {
                    await Task.Delay(currentDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Normal polling loop cancellation requested");
                    break;
                }
            }
        }

        private async Task FastPollingLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Only poll fast if we're in an active countdown phase
                if (!fastPollingActive || file == null)
                {
                    await Task.Delay(normalInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    using var view = file.CreateViewStream();

                    // Seek to StartLights offset and read only that field (4 bytes)
                    view.Position = offsetStartLights;
                    var bytesRead = view.Read(fastReadBuffer!, 0, 4);

                    if (bytesRead == 4)
                    {
                        int currentStartLights = BitConverter.ToInt32(fastReadBuffer!, 0);

                        // Only fire event if value changed
                        if (currentStartLights != lastStartLights)
                        {
                            lastStartLights = currentStartLights;
                            StartLightsChanged?.Invoke(currentStartLights);
                            logger.LogDebug("StartLights changed to: {StartLights}", currentStartLights);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error in fast polling loop");
                    // Don't dispose file here - let the main loop handle that
                    await Task.Delay(normalInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await Task.Delay(fastInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Fast polling loop cancellation requested");
                    break;
                }
            }
        }

        private void UpdateFastPollingState(Shared sharedData)
        {
            var sessionPhase = (Constant.SessionPhase)sharedData.SessionPhase;
            var startLights = sharedData.StartLights;

            bool shouldBeActive =
                (sessionPhase == Constant.SessionPhase.Countdown ||
                 sessionPhase == Constant.SessionPhase.Formation ||
                 sessionPhase == Constant.SessionPhase.Green) &&
                startLights >= 0;

            if (shouldBeActive != fastPollingActive)
            {
                fastPollingActive = shouldBeActive;
                logger.LogInformation("Fast polling {State}", fastPollingActive ? "activated" : "deactivated");

                // Reset lastStartLights when transitioning to active
                if (fastPollingActive)
                {
                    lastStartLights = startLights;
                }
            }
        }

        public override void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            logger.LogInformation("Disposing SharedMemoryService");
            file?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            logger.LogInformation("Disposing SharedMemoryService asynchronously");
            file?.Dispose();

            base.Dispose();

            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
