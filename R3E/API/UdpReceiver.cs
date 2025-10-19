using System.Net;
using System.Net.Sockets;

namespace R3E.API
{
    internal class UdpReceiver : IDisposable
    {
        private readonly int port;
        private readonly UdpClient udpClient;
        private readonly CancellationTokenSource cts = new();
        private readonly TimeSpan timeInterval = TimeSpan.FromMilliseconds(10);

        public event Action<IPEndPoint, byte[]>? DataReceived;

        public UdpReceiver(int port)
        {
            this.port = port;
            udpClient = new UdpClient(this.port);
        }

        public async Task PollLoop(CancellationToken token)
        {
            Console.WriteLine($"[UDP] Listening on port {port}...");

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
                    // Network issue, wait a bit then continue
                    Console.WriteLine($"[UDP Socket Error] {ex.Message}");
                    try { await Task.Delay(timeInterval, token).ConfigureAwait(false); } catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP Error] {ex.Message}");
                    try { await Task.Delay(timeInterval, token).ConfigureAwait(false); } catch { }
                }
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            udpClient?.Dispose();
            cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
