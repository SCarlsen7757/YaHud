using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Interfaces;
using R3E.Data;
using R3E.Networking;

namespace R3E.Core.SharedMemory
{
    // Hosted service that exposes remote shared memory via UDP
    public class RemoteSharedMemoryService : ISharedSource, IHostedService, IAsyncDisposable
    {
        /// <summary>
        /// UDP port used when no <c>--udp-port</c> launch argument is supplied. The relay defaults
        /// to the same value, so both ends stay in sync out of the box.
        /// </summary>
        public const int DefaultUdpPort = 10101;

        private readonly ILogger<RemoteSharedMemoryService> logger;
        private readonly UdpReceiver receiver;
        private readonly int port;
        private Task? pollTask;
        private CancellationTokenSource? receiverCts;
        private bool disposed;

        public event Action<Shared>? DataUpdated;

        public event Action<ReadOnlyMemory<byte>>? RawFrameReceived;

        public event Action<int>? StartLightsChanged;

        public Shared Data { get; private set; }

        public RemoteSharedMemoryService(int port = DefaultUdpPort, ILoggerFactory? loggerFactory = null)
        {
            this.port = port;
            this.logger = loggerFactory?.CreateLogger<RemoteSharedMemoryService>() ?? NullLogger<RemoteSharedMemoryService>.Instance;
            // UdpReceiver logs the port it actually bound to, which is the confirmation that
            // --udp-port took effect, so give it a real logger rather than a null one.
            receiver = new UdpReceiver(port, loggerFactory?.CreateLogger<UdpReceiver>() ?? NullLogger<UdpReceiver>.Instance);
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

            // Raise the raw tap before marshalling so a throwing downstream handler cannot lose the
            // frame for a recorder.
            RawFrameReceived?.Invoke(bytes.AsMemory());

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
