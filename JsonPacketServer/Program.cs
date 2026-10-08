using System.Net;
using System.Net.Sockets;

namespace JsonPacket.Server
{
    internal class Program
    {
        public static async Task Main()
        {
            TcpListener listener = new(IPAddress.Loopback, 7777);
            listener.Start();
            Console.WriteLine("TCP Server is running on port 7777...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                using NetworkStream stream = client.GetStream();

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
                            await PacketTransfer.SendPacketAsync(new ChatPacket { Message = $"[Server] {chatPacket.Message}" }, stream);
                            break;

                        case PacketType.System:
                            SystemPacket? systemPacket = PacketJsonConverter.Deserialize<SystemPacket>(received.Json);
                            await PacketTransfer.SendPacketAsync(new SystemPacket { Command = $"[Server] {systemPacket.Command}" }, stream);
                            break;

                        default:
                            break;
                    }
                }
            }
        }
    }
}