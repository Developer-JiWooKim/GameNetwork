using System.Net.Sockets;
using System.Text;

namespace UdpReceiverLab
{
    internal class Program
    {
        private static async Task Main()
        {
            using UdpClient receiver = new(7778);
            Console.WriteLine("Listening for UDP packets on port 7778...");

            // UDP 기본 실습
            {
                //UdpReceiveResult result = await receiver.ReceiveAsync();
                //string message = Encoding.UTF8.GetString(result.Buffer);
                //Console.WriteLine($"Received Message: {message}");
            }

            await ReceiveMessages(receiver);
        }

        // 반복문으로 Sender로부터 메세지를 계속 받고, 받은 메세지들을 화면에 출력 
        private static async Task ReceiveMessages(UdpClient receiver)
        {
            while (true)
            {
                UdpReceiveResult result = await receiver.ReceiveAsync();

                string message = Encoding.UTF8.GetString(result.Buffer);
                Console.WriteLine($"Received Message from {result.RemoteEndPoint}: {message}");
            }
        }

    }
}
