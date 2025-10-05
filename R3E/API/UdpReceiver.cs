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
                    if (udpClient.Available > 0)
                    {
                        var result = await udpClient.ReceiveAsync(token);
                        DataReceived?.Invoke(result.RemoteEndPoint, result.Buffer);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal on stop
                    break;
                }
                catch (SocketException)
                {
                    // Network issue, continue loop
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP Error] {ex.Message}");
                }

                await Task.Delay(timeInterval, token).ContinueWith(_ => { }, token);
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
