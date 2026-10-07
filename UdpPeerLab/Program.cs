using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UdpPeerLab
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: UdpPeerLab <localPort> <remotePort> <peerName>");
                return;
            }

            if (!int.TryParse(args[0], out int localPort) ||
                !int.TryParse(args[1], out int remotePort) ||
                string.IsNullOrWhiteSpace(args[2]))
            {
                Console.WriteLine("Usage: UdpPeerLab <localPort> <remotePort> <peerName>");
                return;
            }

            string peerName = args[2];
            using UdpClient peer = new(new IPEndPoint(IPAddress.Loopback, localPort));

            IPEndPoint remoteEndPoint = new(IPAddress.Loopback, remotePort);

            Console.WriteLine($"{peerName}: 내 포트 {localPort}, 상대 포트 {remotePort}");
            Console.WriteLine("메세지를 입력하세요. /exit로 종료.");

            using CancellationTokenSource cts = new();

            Task receiveTask = ReceiveMessageAsync(peer, cts);
            Task sendTask = Task.Run(() => SendMessageAsync(peer, remoteEndPoint, cts));

            await Task.WhenAll(receiveTask, sendTask);
        }

        private static async Task SendMessageAsync(UdpClient peer, IPEndPoint remoteEndPoint, CancellationTokenSource cts)
        {
            try
            {
                while (true)
                {
                    string? input = Console.ReadLine();
                    if (input is null || input == "/exit")
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(input);
                        await peer.SendAsync(data, data.Length, remoteEndPoint);
                        Console.WriteLine($"보낸 메세지: {input}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"메세지 전송 중 오류 발생: {ex}");
            }
            finally
            {
                cts.Cancel();
            }
        }

        private static async Task ReceiveMessageAsync(UdpClient peer, CancellationTokenSource cts)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        UdpReceiveResult result = await peer.ReceiveAsync(cts.Token);
                        string receiveMessage = Encoding.UTF8.GetString(result.Buffer);
                        Console.WriteLine($"받은 메세지: {receiveMessage}");
                    }
                    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        // 상대 포트가 닫혀 있을 때 Windows가 알려주는 신호(10054).
                        // UDP에서는 상대가 다시 켜질 수 있으므로 무시하고 계속 받는다.
                        Console.WriteLine("상대 쪽 포트가 닫혀있습니다.");
                    }
                }
            }
            catch (OperationCanceledException exception)
            {
                Console.WriteLine($"수신 종료: {exception.Message}");
            }
            catch (SocketException exception)
            {
                // 취소도, 상대 부재(10054)도 아닌 실제 네트워크 오류
                Console.WriteLine($"수신 오류: {exception.Message}");
            }
            finally
            {
                cts.Cancel();
            }
        }
    }
}