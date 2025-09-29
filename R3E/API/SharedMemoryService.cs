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
        private Shared data = new();
        private readonly Thread? thread;
        private bool running;

        private readonly TimeSpan timeInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Raised when new shared memory data is available.
        /// </summary>
        public event Action<Shared>? DataUpdated;

        public Shared? Data => data;

        public SharedMemoryService()
        {
            running = true;
            if (OperatingSystem.IsWindows())
            {
                thread = new Thread(PollLoop) { IsBackground = true };
                thread.Start();
            }
            else
            {
                //TODO: Add UPD relying service for Linux

                throw new PlatformNotSupportedException("SharedMemoryService is only supported on Windows.");
            }
        }

        [SupportedOSPlatform("windows")]
        private void PollLoop()
        {
            while (running)
            {
                if (Utilities.IsRrreRunning() && file == null)
                {
                    try
                    {
                        file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                        buffer = new byte[Marshal.SizeOf<Shared>()];
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

                Thread.Sleep(timeInterval);
            }
        }

        public void Dispose()
        {
            running = false;
            thread?.Join();
            file?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}