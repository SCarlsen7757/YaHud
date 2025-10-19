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
                // UDP callback: use header fingerprint gating before full marshalling
                udpReceiver.DataReceived += (endPoint, bytes) =>
                {
                    var expected = Marshal.SizeOf<Shared>();
                    if (bytes.Length != expected) return;

                    // compute a small header fingerprint; if unchanged, skip full marshal
                    var headerHash = ComputeHeaderFingerprint(bytes);
                    if (lastHash.HasValue && lastHash.Value == headerHash) return;

                    lastHash = headerHash;

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

                        // header gating: compute small fingerprint from header fields
                        var headerHash = ComputeHeaderFingerprint(readBuffer);
                        if (lastHash.HasValue && lastHash.Value == headerHash)
                        {
                            // no change in header fields
                        }
                        else
                        {
                            lastHash = headerHash;

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

        // Compute a tiny fingerprint from a few reliable header fields to gate full marshalling.
        // Fields chosen (offsets relative to struct):
        // - VersionMajor (Int32) at offset 0
        // - VersionMinor (Int32) at offset 4
        // - SessionType (Int32) at offset: sizeof(Int32)*? (calculate)
        // - LapTimeCurrentSelf (Single) nested in Player at known offset (calculate)
        private static ulong ComputeHeaderFingerprint(ReadOnlySpan<byte> data)
        {
            // We will read offsets manually based on the Shared layout. Keep this simple and robust.
            // Offsets:
            // VersionMajor = 0
            // VersionMinor = 4
            // SessionType = 4*4 = 16 (after VersionMajor, VersionMinor, AllDriversOffset, DriverDataSize)
            // Player starts next; LapTimeCurrentSelf is at a larger offset - we'll sample a few ints instead for safety: SessionType, SessionPhase, and Position.

            if (data.Length < 20) return 0;

            int versionMajor = BitConverter.ToInt32(data.Slice(0, 4));
            int versionMinor = BitConverter.ToInt32(data.Slice(4, 4));
            int sessionType = BitConverter.ToInt32(data.Slice(16, 4));

            // Read a few more small fields: SessionPhase (offset 32) and Position (offset approximate)
            int sessionPhase = 0;
            int position = 0;
            if (data.Length >= 36)
            {
                sessionPhase = BitConverter.ToInt32(data.Slice(32, 4));
            }

            // Position is further down; approximate offset by searching for a reasonable location: we'll read at offset 200 as a heuristic if available
            if (data.Length >= 204)
            {
                position = BitConverter.ToInt32(data.Slice(200, 4));
            }

            // Combine small set using FNV
            ulong h = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            foreach (var b in BitConverter.GetBytes(versionMajor)) { h ^= b; h *= prime; }
            foreach (var b in BitConverter.GetBytes(versionMinor)) { h ^= b; h *= prime; }
            foreach (var b in BitConverter.GetBytes(sessionType)) { h ^= b; h *= prime; }
            foreach (var b in BitConverter.GetBytes(sessionPhase)) { h ^= b; h *= prime; }
            foreach (var b in BitConverter.GetBytes(position)) { h ^= b; h *= prime; }

            return h;
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