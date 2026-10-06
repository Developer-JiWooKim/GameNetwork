using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace TcpServerLab
{
    internal class Program
    {
        private static readonly List<TcpSession> Clients = new();
        private static readonly object _clientLock = new();

        public static async Task Main(string[] args)
        {
            // Ctrl+C를 누르면 프로세스를 바로 죽이지 않고 토큰으로 서버 전체에 종료를 알린다
            using CancellationTokenSource cts = new();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            TcpListener listener = new(IPAddress.Loopback, 7777);
            listener.Start();

            Console.WriteLine("TCP Server is running on port 7777... (Ctrl+C to stop)");

            // Tcp 응용 실습(다중 클라이언트 채팅)
            {
                List<Task> handlers = new();

                try
                {
                    while (true)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync(cts.Token);

                        handlers.RemoveAll(t => t.IsCompleted);
                        handlers.Add(HandleClientAsync(client, cts.Token));
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Server is shutting down...");
                }
                finally
                {
                    listener.Stop();
                }

                // 각 클라이언트 처리가 토큰으로 정리를 마칠 때까지 기다린 뒤 종료
                await Task.WhenAll(handlers);
                Console.WriteLine("Server stopped.");
            }

            // Tcp 기본 실습
            {
                // using TcpClient client = await listener.AcceptTcpClientAsync();
                // using NetworkStream stream = client.GetStream();
                // using StreamReader reader = new(stream);
                // using StreamWriter writer = new(stream) { AutoFlush = true };

                // string? message = await reader.ReadLineAsync();
                // Console.WriteLine($"Received: {message}");

                // await writer.WriteLineAsync($"PONG");

                // listener.Stop();
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                using NetworkStream stream = client.GetStream();
                using StreamReader reader = new(stream);
                using StreamWriter writer = new(stream) { AutoFlush = true };

                TcpSession session = null;
                Task sendTask = Task.CompletedTask;

                try
                {
                    string? nickname = await reader.ReadLineAsync(token);
                    if (string.IsNullOrEmpty(nickname))
                    {
                        return;
                    }
                    session = new TcpSession(nickname, client, writer);
                    sendTask = session.RunSendLoopAsync();

                    lock (_clientLock)
                    {
                        Clients.Add(session);
                    }

                    BroadcastMessage($"{nickname} has joined the chat.");

                    while (true)
                    {
                        string? message = await reader.ReadLineAsync(token);
                        if (message is null)
                        {
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            BroadcastMessage($"[{nickname}]: {message}", session);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 서버 종료 요청: 정상 흐름이므로 에러로 취급하지 않고 finally에서 정리
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred: {ex.Message}");
                }
                finally
                {
                    if (session is not null)
                    {
                        lock (_clientLock)
                        {
                            Clients.Remove(session);
                        }

                        // 큐를 닫고 소켓을 닫아 송신 루프를 깨운 뒤, 루프가 끝나면 정리 진행
                        session.Close();
                        await sendTask;

                        Console.WriteLine($"{session.NickName} has disconnected");
                    }
                }
            }
        }

        private static void BroadcastMessage(string message, TcpSession? except = null)
        {
            TcpSession[] sessions = null;
            lock (_clientLock)
            {
                sessions = Clients.ToArray();
            }

            foreach (TcpSession client in sessions)
            {
                if (client == except)
                {
                    continue;
                }

                // 큐에 넣기만 하고 바로 반환 → 느린 클라이언트가 다른 클라이언트를 막지 않음
                if (!client.TrySend(message))
                {
                    // 큐가 가득 찼다 = 수신이 너무 느린 클라이언트 → 끊어서 메모리 폭증 방지
                    Console.WriteLine($"{client.NickName}: send queue full, disconnecting");
                    client.Close();
                }
            }
        }

        private sealed class TcpSession
        {
            private const int MaxQueueSize = 100;

            private readonly TcpClient _client;
            private readonly StreamWriter _writer;
            private readonly Channel<string> _queue = Channel.CreateBounded<string>(
                new BoundedChannelOptions(MaxQueueSize)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait
                });

            public string NickName { get; set; }

            public TcpSession(string nickName, TcpClient client, StreamWriter writer)
            {
                NickName = nickName;
                _client = client;
                _writer = writer;
            }

            public bool TrySend(string message)
            {
                return _queue.Writer.TryWrite(message);
            }

            // 세션당 하나만 도는 송신 루프: 이 루프만 _writer에 쓰므로 쓰기가 자동으로 직렬화됨
            public async Task RunSendLoopAsync()
            {
                try
                {
                    await foreach (string message in _queue.Reader.ReadAllAsync())
                    {
                        await _writer.WriteLineAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{NickName}] send failed: {ex.Message}");
                    Close();
                }
            }

            public void Close()
            {
                _queue.Writer.TryComplete();
                _client.Close();
            }
        }
    }
}
