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
        private readonly ILogger<SharedMemoryService> logger;

        private readonly TimeSpan normalInterval = TimeSpan.FromMilliseconds(16); // ~60Hz
        private readonly TimeSpan pausedInterval = TimeSpan.FromMilliseconds(200); // Game paused update interval
        private readonly TimeSpan notRunningInterval = TimeSpan.FromMilliseconds(5000); // Game not running update interval

        // Offsets computed at runtime so code remains correct if SHM layout moves
        private static readonly int offsetPlayer;
        private static readonly int offsetGameSimulationTicks;

        // Reusable buffer for reading from the memory mapped file to avoid per-frame allocations
        private byte[]? readBuffer;
        private bool disposed;

        static SharedMemoryService()
        {
            // Use Marshal.OffsetOf to compute offsets relative to the Shared struct
            offsetPlayer = (int)Marshal.OffsetOf<Shared>(nameof(Shared.Player));

            // Offset of GameSimulationTicks inside PlayerData
            offsetGameSimulationTicks = offsetPlayer + (int)Marshal.OffsetOf<PlayerData>(nameof(PlayerData.GameSimulationTicks));
        }

        /// <summary>
        /// Raised when new shared memory data is available.
        /// </summary>
        public event Action<Shared>? DataUpdated;

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

            // allocate read buffer once
            readBuffer = new byte[expected];

            // current delay, start with normal
            var currentDelay = normalInterval;

            logger.LogInformation("Starting shared memory poll loop (expected {Size} bytes)", expected);

            ulong noUpdate = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        logger.LogInformation("Opened shared memory '{Name}'", Constant.SharedMemoryName);
                        // reset to normal when found
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
                        while (bytesRead < expected)
                        {
                            var read = view.Read(readBuffer, bytesRead, expected - bytesRead);
                            if (read <= 0) break; // incomplete read
                            bytesRead += read;
                        }

                        if (bytesRead != expected)
                        {
                            // skip incomplete read
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
                            // update last observed
                            lastSimTicks = simTicks;

                            if (SharedMarshaller.TryMarshalShared(readBuffer, out var newData))
                            {
                                data = newData;
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
                        // if file lost, back off heavily
                        currentDelay = notRunningInterval;
                    }
                }
                else
                {
                    // game not running - back off to reduce CPU
                    currentDelay = notRunningInterval;
                }

                try
                {
                    await Task.Delay(currentDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Shared memory poll loop cancellation requested");
                    break;
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

            // BackgroundService doesn't implement IAsyncDisposable, so just dispose synchronously
            base.Dispose();

            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
