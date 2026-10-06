using System.Net;
using System.Net.Sockets;

namespace TcpClientLab
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Console.Write("NickName: ");

            string? input = Console.ReadLine();
            string nickname = string.IsNullOrWhiteSpace(input) ? "Annonymous" : input.Trim();

            using TcpClient client = new();
            await client.ConnectAsync(IPAddress.Loopback, 7777);

            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream) { AutoFlush = true };

            // Tcp 응용 실습(다중 클라이언트 채팅)
            {
                await writer.WriteLineAsync(nickname);

                // 종료 신호: 입력 루프와 수신 루프 어느 쪽이 끝나든 이 토큰으로 상대 루프를 멈춘다
                using CancellationTokenSource cts = new();

                Task recieveTask = RecieveMessageAsync(reader, cts);

                Console.WriteLine("Start Input message");

                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        string? message = await ReadConsoleLineAsync(cts.Token);
                        if (message is null || message == "/exit")
                        {
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            await writer.WriteLineAsync(message);
                        }
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Send failed: {ex.Message}");
                }

                cts.Cancel();
                client.Close();

                await recieveTask;
            }

            // Tcp 기본 실습
            {
                // string? response = await reader.ReadLineAsync();
                // Console.WriteLine($"Received: {response}");
            }
        }

        private static async Task RecieveMessageAsync(StreamReader reader, CancellationTokenSource cts)
        {
            try
            {
                while (true)
                {
                    string? message = await reader.ReadLineAsync(cts.Token);
                    if (message is null || message == "/exit")
                    {
                        Console.WriteLine("Disconnected from server.");
                        break;
                    }
                    Console.WriteLine(message);
                }
            }
            catch (OperationCanceledException)
            {
                // 내가 종료를 요청한 정상 흐름: 메시지 출력 없음
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                // 서버가 먼저 끊긴 경우, 입력 대기 중인 Main도 깨운다
                cts.Cancel();
            }
        }

        // Console.ReadLine은 취소할 수 없으므로 별도 스레드에서 읽고, 취소 신호가 먼저 오면 포기한다
        private static async Task<string?> ReadConsoleLineAsync(CancellationToken token)
        {
            Task<string?> readTask = Task.Run(() => Console.ReadLine());
            Task cancelTask = Task.Delay(Timeout.Infinite, token);

            Task finished = await Task.WhenAny(readTask, cancelTask);
            return finished == readTask ? await readTask : null;
        }
    }
}
