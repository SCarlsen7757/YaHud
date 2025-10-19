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
        private ulong? lastHash;

        private readonly TimeSpan timeInterval = TimeSpan.FromMilliseconds(16);

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
                // TODO: Investigate whether UdpReceiver reuses buffers; if so copy before using
                udpReceiver.DataReceived += (endPoint, bytes) =>
                {
                    var expected = Marshal.SizeOf<Shared>();
                    if (bytes.Length != expected) return;

                    // compute a small-sample checksum instead of hashing the whole buffer
                    var hash = ComputeSampleChecksum(bytes);

                    // if identical to last hash, nothing changed
                    if (lastHash.HasValue && lastHash.Value == hash) return;

                    // update stored hash
                    lastHash = hash;

                    // Marshall and publish
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

            // adaptive delay when game not running
            var currentDelay = timeInterval;
            var backoffMs = 250; // start backoff when game not found

            while (!token.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    Console.WriteLine("Raceroom game found");
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        Console.WriteLine("SHM file found");
                        // reset adaptive delay when file found
                        currentDelay = timeInterval;
                        backoffMs = 250;
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

                        // Compute a sample checksum (cheap) rather than hashing the whole buffer
                        var hash = ComputeSampleChecksum(readBuffer);
                        if (lastHash.HasValue && lastHash.Value == hash)
                        {
                            // no change
                        }
                        else
                        {
                            lastHash = hash;

                            if (TryMarshalShared(readBuffer, out var newData))
                            {
                                data = newData;
                                DataUpdated?.Invoke(data);
                            }
                        }
                    }
                    catch
                    {
                        file?.Dispose();
                        file = null;
                        // if file lost, increase backoff
                        currentDelay = TimeSpan.FromMilliseconds(backoffMs);
                        backoffMs = Math.Min(backoffMs * 2, 2000);
                    }
                }
                else
                {
                    // game not running - back off to reduce CPU
                    currentDelay = TimeSpan.FromMilliseconds(backoffMs);
                    backoffMs = Math.Min(backoffMs * 2, 2000);
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

        // Compute a small-sample checksum from a few regions of the buffer to avoid hashing the full buffer.
        // This is conservative and low-risk: it does not change the Shared memory layout and is cheap.
        private static ulong ComputeSampleChecksum(ReadOnlySpan<byte> data, int sampleSize = 256)
        {
            if (data.Length <= sampleSize) return ComputeChecksum(data);

            // sample first N bytes, middle N bytes, last N bytes (or as much as available)
            var hash = 14695981039346656037UL;
            int region = sampleSize / 3;
            if (region <= 0) region = Math.Min(sampleSize, 64);

            // first
            hash = CombineFnv(hash, data.Slice(0, region));

            // middle
            int midStart = Math.Max(0, (data.Length / 2) - region / 2);
            hash = CombineFnv(hash, data.Slice(midStart, region));

            // last
            hash = CombineFnv(hash, data.Slice(data.Length - region, region));

            return hash;
        }

        private static ulong CombineFnv(ulong seed, ReadOnlySpan<byte> span)
        {
            const ulong prime = 1099511628211UL;
            ulong h = seed;
            for (int i = 0; i < span.Length; i++)
            {
                h ^= span[i];
                h *= prime;
            }
            return h;
        }

        // Fast non-cryptographic 64-bit FNV-1a for change detection
        private static ulong ComputeChecksum(ReadOnlySpan<byte> data)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= prime;
            }
            return hash;
        }

        // Overload for byte[] to make caller code simple
        private static ulong ComputeChecksum(byte[] data) => ComputeChecksum((ReadOnlySpan<byte>)data);

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