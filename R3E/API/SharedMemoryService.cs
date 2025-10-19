using R3E.Data;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace R3E.API
{
    public class SharedMemoryService : IDisposable, IAsyncDisposable
    {
        private MemoryMappedFile? file;
        private Shared data;
        private readonly CancellationTokenSource cts = new();
        private readonly Task? pollingTask;
        private readonly UdpReceiver? udpReceiver;

        // Track last observed header values for change detection
        private int? lastSimTicks;
        private int? lastGamePaused;

        private readonly TimeSpan normalInterval = TimeSpan.FromMilliseconds(16); // ~60Hz
        private readonly TimeSpan pausedInterval = TimeSpan.FromMilliseconds(200); // when game paused
        private readonly TimeSpan notRunningInterval = TimeSpan.FromMilliseconds(5000); // when game not running

        // Offsets computed at runtime so code remains correct if SHM layout moves
        private static readonly int s_offsetGamePaused;
        private static readonly int s_offsetPlayer;
        private static readonly int s_offsetGameSimulationTicks;

        static SharedMemoryService()
        {
            // Use Marshal.OffsetOf to compute offsets relative to the Shared struct
            s_offsetGamePaused = (int)Marshal.OffsetOf<Shared>(nameof(Shared.GamePaused));
            s_offsetPlayer = (int)Marshal.OffsetOf<Shared>(nameof(Shared.Player));

            // Offset of GameSimulationTicks inside PlayerData
            s_offsetGameSimulationTicks = s_offsetPlayer + (int)Marshal.OffsetOf<PlayerData>(nameof(PlayerData.GameSimulationTicks));
        }

        /// <summary>
        /// Raised when new shared memory data is available.
        /// </summary>
        public event Action<Shared>? DataUpdated;

        public Shared Data => data;

        private readonly bool useUdp = !OperatingSystem.IsWindows();

        // Reusable buffer for reading from the memory mapped file to avoid per-frame allocations
        private byte[]? readBuffer;

        public SharedMemoryService() : this(false) { }

        public SharedMemoryService(bool? useUdp)
        {
            if (useUdp is not null)
            {
                this.useUdp = useUdp.Value;
            }

            data = new();
            if (OperatingSystem.IsWindows() && !this.useUdp)
            {
                pollingTask = Task.Run(() => PollLoop(cts.Token));
            }
            else
            {
                udpReceiver = new UdpReceiver(10101);
                // UDP callback: use GameSimulationTicks + GamePaused gating before full marshalling
                udpReceiver.DataReceived += (endPoint, bytes) =>
                {
                    var expected = Marshal.SizeOf<Shared>();
                    if (bytes.Length != expected) return;

                    // Ensure we have enough bytes to read the header fields
                    if (bytes.Length < s_offsetGameSimulationTicks + 4) return;

                    // Read GamePaused and Player.GameSimulationTicks using computed offsets
                    int gamePaused = BitConverter.ToInt32(bytes, s_offsetGamePaused);
                    int simTicks = BitConverter.ToInt32(bytes, s_offsetGameSimulationTicks);

                    // If nothing changed, skip
                    if (lastSimTicks.HasValue && lastGamePaused.HasValue && lastSimTicks.Value == simTicks && lastGamePaused.Value == gamePaused)
                    {
                        return;
                    }

                    // update last observed
                    lastSimTicks = simTicks;
                    lastGamePaused = gamePaused;

                    // If game is paused, skip publishing (HUD hidden)
                    if (gamePaused != 0)
                    {
                        return;
                    }

                    // marshal and publish
                    if (TryMarshalShared(bytes, out var newData))
                    {
                        data = newData;
                        DataUpdated?.Invoke(data);
                    }
                };
                pollingTask = Task.Run(() => udpReceiver.PollLoop(cts.Token));
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task PollLoop(CancellationToken token)
        {
            var expected = Marshal.SizeOf<Shared>();

            // allocate read buffer once
            readBuffer = new byte[expected];

            // current delay, start with normal
            var currentDelay = normalInterval;

            while (!token.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
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
                            await Task.Delay(currentDelay, token).ConfigureAwait(false);
                            continue;
                        }

                        // Determine paused state and sim ticks directly from buffer using computed offsets
                        int gamePaused = 0;
                        int simTicks = 0;
                        if (readBuffer.Length >= s_offsetGameSimulationTicks + 4)
                        {
                            gamePaused = BitConverter.ToInt32(readBuffer, s_offsetGamePaused);
                            simTicks = BitConverter.ToInt32(readBuffer, s_offsetGameSimulationTicks);
                        }

                        // adjust polling interval based on paused/running
                        currentDelay = gamePaused != 0 ? pausedInterval : normalInterval;

                        // If nothing changed, skip
                        if (lastSimTicks.HasValue && lastGamePaused.HasValue && lastSimTicks.Value == simTicks && lastGamePaused.Value == gamePaused)
                        {
                            // no change, continue
                        }
                        else
                        {
                            // update last observed
                            lastSimTicks = simTicks;
                            lastGamePaused = gamePaused;

                            // If game is paused, skip publishing (HUD hidden)
                            if (gamePaused != 0)
                            {
                                // do not marshal or publish while paused
                            }
                            else
                            {
                                if (TryMarshalShared(readBuffer, out var newData))
                                {
                                    data = newData;
                                    // Immediately publish updates so HUD receives fresh data
                                    DataUpdated?.Invoke(data);
                                }
                            }
                        }
                    }
                    catch
                    {
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
                    await Task.Delay(currentDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // cancellation requested
                }
            }
        }

        private static bool TryMarshalShared(byte[] src, out Shared value)
        {
            value = new();
            if (src.Length < Marshal.SizeOf<Shared>()) return false;

            // Use the reliable GCHandle + Marshal.PtrToStructure approach
            var handle = GCHandle.Alloc(src, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                value = Marshal.PtrToStructure<Shared>(ptr);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                cts.Cancel();

                if (pollingTask != null)
                {
                    try
                    {
                        await pollingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                    catch { /* swallow any other exceptions during shutdown */ }
                }

                file?.Dispose();
                udpReceiver?.Dispose();
            }
            finally
            {
                cts.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}