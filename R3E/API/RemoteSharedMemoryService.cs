using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using R3E.Data;

namespace R3E.API
{
    // Hosted service that exposes remote shared memory via UDP
    public class RemoteSharedMemoryService : ISharedSource, IHostedService, IDisposable
    {
        private readonly ILogger<RemoteSharedMemoryService> logger;
        private readonly UdpReceiver receiver;
        private readonly int port = 10101;

        public event Action<Shared>? DataUpdated;

        public Shared Data { get; private set; }

        public RemoteSharedMemoryService(ILogger<RemoteSharedMemoryService>? logger = null)
        {
            this.logger = logger ?? NullLogger<RemoteSharedMemoryService>.Instance;
            receiver = new UdpReceiver(port, NullLogger<UdpReceiver>.Instance);
            receiver.DataReceived += OnDataReceived;
            Data = new Shared();
        }

        private void OnDataReceived(System.Net.IPEndPoint ep, byte[] bytes)
        {
            var expected = System.Runtime.InteropServices.Marshal.SizeOf<Shared>();
            if (bytes.Length != expected) return;
            if (SharedMemoryService.TryMarshalShared(bytes, out var newData))
            {
                Data = newData;
                DataUpdated?.Invoke(Data);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // start receiver loop
            _ = receiver.PollLoop(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            receiver.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            receiver.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
