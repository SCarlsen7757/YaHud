using R3E.Data;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

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
                udpReceiver.DataReceived += (endPoint, bytes) =>
                {
                    var expected = Marshal.SizeOf<Shared>();
                    if (bytes.Length != expected) return;

                    // compute checksum of the incoming data (using MD5 then taking first 8 bytes)
                    var hash = ComputeChecksum(bytes);

                    // if identical to last hash, nothing changed
                    if (lastHash.HasValue && lastHash.Value == hash) return;

                    // update stored hash
                    lastHash = hash;

                    // copy before marshalling because receiver may reuse its buffer
                    var buf = new byte[bytes.Length];
                    Array.Copy(bytes, buf, bytes.Length);

                    if (TryMarshalShared(buf, out var newData))
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

            while (!token.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    Console.WriteLine("Raceroom game found");
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        Console.WriteLine("SHM file found");
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
                        using var stream = new BinaryReader(view);
                        var read = stream.ReadBytes(expected);
                        if (read.Length != expected)
                        {
                            // skip incomplete read
                            continue;
                        }

                        var hash = ComputeChecksum(read);
                        if (lastHash.HasValue && lastHash.Value == hash)
                        {
                            // no change
                        }
                        else
                        {
                            lastHash = hash;

                            if (TryMarshalShared(read, out var newData))
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
                    }
                }

                try
                {
                    await Task.Delay(timeInterval, token).ConfigureAwait(false);
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

        private static ulong ComputeChecksum(byte[] data)
        {
            // MD5 is available in System.Security.Cryptography and fast enough for change-detection.
            // We take the first 8 bytes of the MD5 hash as a 64-bit fingerprint.
            var digest = MD5.HashData(data);
            // ensure there are at least 8 bytes (MD5 is 16 bytes)
            return BitConverter.ToUInt64(digest, 0);
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