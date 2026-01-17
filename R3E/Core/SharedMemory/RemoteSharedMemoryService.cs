using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Interfaces;
using R3E.Data;
using R3E.Networking;

namespace R3E.Core.SharedMemory
{
    // Hosted service that exposes remote shared memory via UDP
    public class RemoteSharedMemoryService : ISharedSource, IHostedService, IAsyncDisposable
    {
        private readonly ILogger<RemoteSharedMemoryService> logger;
        private readonly UdpReceiver receiver;
        private readonly int port = 10101;
        private Task? pollTask;
        private CancellationTokenSource? receiverCts;
        private bool disposed;

        public event Action<Shared>? DataUpdated;

        public event Action<int>? StartLightsChanged;

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

            if (pollTask != null)
                throw new InvalidOperationException("Service is already started");

            // Create a dedicated CancellationTokenSource for the receiver
            receiverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start receiver loop with proper fire-and-forget pattern
            pollTask = Task.Run(async () =>
            {
                try
                {
                    await receiver.PollLoop(receiverCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    logger.LogDebug("PollLoop cancelled gracefully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in UDP receiver poll loop");
                    throw; // Re-throw to ensure the task enters faulted state
                }
            }, receiverCts.Token);

            _ = pollTask.ContinueWith(t => logger.LogError(t.Exception, "PollLoop terminated unexpectedly"), TaskContinuationOptions.OnlyOnFaulted);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping RemoteSharedMemoryService");

            // Signal cancellation
            receiverCts?.Cancel();

            if (pollTask != null)
            {
                try
                {
                    // Wait for the pollTask to complete with a timeout
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                    await pollTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on timeout or cancellation
                    logger.LogWarning("PollTask did not complete within timeout period");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception while stopping UDP receiver");
                }
            }

            receiver.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            logger.LogDebug("Disposing RemoteSharedMemoryService");

            receiverCts?.Cancel();

            // Wait for the poll task to complete with a timeout
            if (pollTask is { IsCompleted: false })
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await pollTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected - task was cancelled or timed out
                    logger.LogWarning("PollTask did not complete within disposal timeout");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception while disposing UDP receiver");
                }
            }

            receiverCts?.Dispose();
            receiver.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
