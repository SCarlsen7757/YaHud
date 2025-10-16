using R3E.Data;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace R3E.API
{
    public class SharedMemoryService : IDisposable
    {
        private MemoryMappedFile? file;
        private byte[]? buffer;
        private Shared data;
        private readonly CancellationTokenSource cts = new();
        private readonly Task? pollingTask;
        private readonly UdpReceiver? udpReceiver;

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
                    if (bytes.Length == Marshal.SizeOf<Shared>())
                    {
                        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        var newData = Marshal.PtrToStructure<Shared>(handle.AddrOfPinnedObject());
                        handle.Free();
                        if (!Equals(data, newData))
                        {
                            data = newData;
                            DataUpdated?.Invoke(data);
                        }
                    }
                };
                pollingTask = Task.Run(() => udpReceiver.PollLoop(cts.Token));
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    Console.WriteLine("Raceroom game found");
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        buffer = new byte[Marshal.SizeOf<Shared>()];
                        Console.WriteLine("SHM file found");
                    }
                    catch (FileNotFoundException)
                    {
                        file = null;
                        Console.WriteLine("SHM file not found");
                    }
                }

                if (file != null)
                {
                    try
                    {
                        using var view = file.CreateViewStream();
                        using var stream = new BinaryReader(view);
                        buffer = stream.ReadBytes(Marshal.SizeOf<Shared>());
                        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        var newData = Marshal.PtrToStructure<Shared>(handle.AddrOfPinnedObject());
                        handle.Free();

                        if (!Equals(data, newData))
                        {
                            data = newData;
                            DataUpdated?.Invoke(data);
                        }
                    }
                    catch
                    {
                        file?.Dispose();
                        file = null;
                    }
                }

                await Task.Delay(timeInterval, token).ContinueWith(_ => { }, token);
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            pollingTask?.Wait(); //Error here
            file?.Dispose();
            cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}