using System.Net;
using System.Net.Sockets;

namespace R3E.UdpRelay
{
    internal class UdpRelayService(int sourcePort, string targetAddress, int targetPort) : IDisposable
    {
        private readonly UdpClient udpClient = new();
        private readonly CancellationTokenSource cts = new();

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
            return udpClient.SendAsync(data, data.Length, TargetEndpoint);
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
