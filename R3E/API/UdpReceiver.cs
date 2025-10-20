using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace R3E.API
{
    internal class UdpReceiver : IDisposable
    {
        private readonly int port;
        private readonly UdpClient udpClient;
        private readonly CancellationTokenSource cts = new();
        private readonly ILogger<UdpReceiver> logger;

        public event Action<IPEndPoint, byte[]>? DataReceived;

        public UdpReceiver(int port, ILogger<UdpReceiver>? logger = null)
        {
            this.port = port;
            this.logger = logger ?? NullLogger<UdpReceiver>.Instance;
            udpClient = new UdpClient(this.port);
        }

        public async Task PollLoop(CancellationToken token)
        {
            logger.LogInformation("Listening on UDP port {Port}", port);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(token).ConfigureAwait(false);
                    DataReceived?.Invoke(result.RemoteEndPoint, result.Buffer);
                }
                catch (OperationCanceledException)
                {
                    // Normal on stop
                    break;
                }
                catch (SocketException ex)
                {
                    logger.LogWarning(ex, "UDP socket exception");
                    // Network issue, continue loop
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "UDP receive loop error");
                }
            }
        }

        public void Dispose()
        {
            logger.LogDebug("Disposing UdpReceiver on port {Port}", port);
            cts.Cancel();
            udpClient?.Dispose();
            cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
