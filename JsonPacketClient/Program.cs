using System.Net;
using System.Net.Sockets;

namespace JsonPacket.Client
{
    internal class Program
    {
        public static async Task Main()
        {
            using TcpClient client = new();
            await client.ConnectAsync(IPAddress.Loopback, 7777);

            using NetworkStream stream = client.GetStream();

            await PacketTransfer.SendPacketAsync(new SystemPacket { Command = "JOIN" }, stream);
            await PacketTransfer.SendPacketAsync(new ChatPacket { Message = "Hello, World!" }, stream);

            while (true)
            {
                ReceivedPacket? received = await PacketTransfer.ReceivePacketAsync(stream);
                if (received is null)
                {
                    break;
                }

                switch (received.Type)
                {
                    case PacketType.Chat:
                        ChatPacket? chatPacket = PacketJsonConverter.Deserialize<ChatPacket>(received.Json);
                        Console.WriteLine($"[CHAT] {chatPacket.Message}");
                        break;

                    case PacketType.System:
                        SystemPacket? systemPacket = PacketJsonConverter.Deserialize<SystemPacket>(received.Json);
                        Console.WriteLine($"[SYSTEM] {systemPacket.Command}");
                        break;

                    default:
                        Console.WriteLine("[UNKNOWN] Unknown packet type received.");
                        break;
                }
            }
        }
    }
}