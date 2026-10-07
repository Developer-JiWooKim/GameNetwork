using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UdpSenderLab
{
    internal class Program
    {
        private static async Task Main()
        {
            using UdpClient sender = new();
            IPEndPoint receiverEndPoint = new(IPAddress.Loopback, 7778);

            // UDP 기본 실습
            {
                //byte[] bytes = Encoding.UTF8.GetBytes("Hello, UDP! ");
                //await sender.SendAsync(bytes, bytes.Length, receiverEndPoint);
                //Console.WriteLine("Message sent to UDP receiver on port 7778.");
            }

            await SendMessages(sender, receiverEndPoint);
        }

        // 반복문으로 Receiver에게 메세지를 계속 보냄
        private static async Task SendMessages(UdpClient sender, IPEndPoint receiverEndPoint)
        {
            Console.WriteLine("Type a message and press Enter to send. (empty line to quit)");

            while (true)
            {
                string? input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    break;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(input);
                await sender.SendAsync(bytes, bytes.Length, receiverEndPoint);

                Console.WriteLine("Message sent to UDP receiver on port 7778.");
            }
        }

    }
}
