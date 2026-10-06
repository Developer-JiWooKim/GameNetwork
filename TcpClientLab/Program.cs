using System.Net;
using System.Net.Sockets;

namespace TcpClientLab
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            using TcpClient client = new();
            await client.ConnectAsync(IPAddress.Loopback, 7777);

            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream) { AutoFlush = true };

            await writer.WriteLineAsync("PING");

            string? response = await reader.ReadLineAsync();
            Console.WriteLine($"Received: {response}");
        }
    }
}