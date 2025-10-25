using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace R3E.UdpRelay
{
    internal class UdpRelayService(int sourcePort, string targetAddress, int targetPort, ILogger<UdpRelayService>? logger = null) : IDisposable
    {
        private readonly UdpClient udpClient = new();
        private readonly CancellationTokenSource cts = new();
        private readonly ILogger<UdpRelayService> logger = logger ?? NullLogger<UdpRelayService>.Instance;
        private bool disposed;

        /// <summary>
        /// Source port to listen on.
        /// </summary>
        public int SourcePort { get; } = sourcePort;

        /// <summary>
        /// Target endpoint to relay data to.
        /// </summary>
        public IPEndPoint TargetEndpoint { get; } = new IPEndPoint(IPAddress.Parse(targetAddress), targetPort);

        public Task SendAsync(byte[] data)
        {
            logger.LogDebug("Sending {Length} bytes to {Endpoint}", data.Length, TargetEndpoint);
            return udpClient.SendAsync(data, data.Length, TargetEndpoint);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            logger.LogDebug("Disposing UdpRelayService");
            cts.Cancel();
            cts.Dispose();
            udpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
