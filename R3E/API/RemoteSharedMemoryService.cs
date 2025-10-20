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
        private Task? pollTask;

        public event Action<Shared>? DataUpdated;

        public Shared Data { get; private set; }

        public RemoteSharedMemoryService(ILogger<RemoteSharedMemoryService>? logger = null)
        {
            this.logger = logger ?? NullLogger<RemoteSharedMemoryService>.Instance;
            receiver = new UdpReceiver(port, NullLogger<UdpReceiver>.Instance);
            receiver.DataReceived += OnDataReceived;
            Data = new Shared();
            this.logger.LogDebug("RemoteSharedMemoryService constructed, listening on port {Port}", port);
        }

        private void OnDataReceived(System.Net.IPEndPoint ep, byte[] bytes)
        {
            var expected = System.Runtime.InteropServices.Marshal.SizeOf<Shared>();
            if (bytes.Length != expected)
            {
                logger.LogDebug("Received UDP packet of unexpected size {Size} from {Endpoint}", bytes.Length, ep);
                return;
            }

            if (SharedMarshaller.TryMarshalShared(bytes, out var newData))
            {
                Data = newData;
                DataUpdated?.Invoke(Data);
                logger.LogDebug("Published remote shared update from {Endpoint}", ep);
            }
            else
            {
                logger.LogWarning("Failed to marshal shared data received from {Endpoint}", ep);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting RemoteSharedMemoryService (UDP receiver) on port {Port}", port);
            // start receiver loop
            pollTask = receiver.PollLoop(cancellationToken);
            // ensure exceptions are observed
            pollTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    logger.LogError(t.Exception!, "Unhandled exception in pollTask");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping RemoteSharedMemoryService");
            receiver.Dispose();
            if (pollTask != null)
            {
                try
                {
                    // wait for the pollTask to complete, with a timeout
                    await Task.WhenAny(pollTask, Task.Delay(5000, cancellationToken));
                }
                catch (TaskCanceledException)
                {
                    // expected on shutdown
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Exception observed in pollTask");
                }
            }
        }

        public void Dispose()
        {
            logger.LogDebug("Disposing RemoteSharedMemoryService");
            receiver.Dispose();
            // if pollTask is still running, wait briefly to avoid blocking too long
            if (pollTask != null && !pollTask.IsCompleted)
            {
                try
                {
                    pollTask.Wait(1000);
                }
                catch
                {
                    // ignore
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
