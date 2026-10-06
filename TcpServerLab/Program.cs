using System.Net;
using System.Net.Sockets;

namespace TcpServerLab
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            TcpListener listener = new(IPAddress.Loopback, 7777);
            listener.Start();

            Console.WriteLine("TCP Server is running on port 7777...");

            using TcpClient client = await listener.AcceptTcpClientAsync();

            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream) { AutoFlush = true };

            string? message = await reader.ReadLineAsync();
            Console.WriteLine($"Received: {message}");

            await writer.WriteLineAsync($"PONG");

            listener.Stop();
        }
    }
}