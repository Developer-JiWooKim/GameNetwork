namespace AysncTask
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // 단순 Task 실습
            {
                // Console.WriteLine("접속 요청 시작");
                // string result = await ConnectAsync();
                // Console.WriteLine(result);
            }

            // Cancellation Token 실습
            {
                //     CancellationTokenSource cts1 = new();
                //     cts1.CancelAfter(TimeSpan.FromSeconds(5));
                //     CancellationTokenPractice tokenPractice = new();
                //     try
                //     {
                //         await tokenPractice.WaitForConnectionAsync(cts1.Token);
                //         Console.WriteLine("접속 완료");
                //     }
                //     catch (OperationCanceledException)
                //     {
                //         Console.WriteLine("접속 요청 취소");
                //     }
                //     cts1.Dispose();
            }

            // WhenAll, CancellationToken 복합 응용 실습
            {
                // try
                // {
                //     using CancellationTokenSource cts2 = new();
                //     cts2.CancelAfter(TimeSpan.FromSeconds(3));
                //     WaitTogether together = new();

                //     Task<bool> taskProfile = together.LoadProfileAsync(cts2.Token);
                //     Task<bool> taskInventroy = together.LoadInventoryAsync(cts2.Token);

                //     bool[] results = await Task.WhenAll(taskProfile, taskInventroy);
                //     bool isComplete = true;
                //     for (int i = 0; i < results.Length; i++)
                //     {
                //         if (!results[i])
                //         {
                //             isComplete = false;
                //             break;
                //         }
                //     }

                //     Console.WriteLine("성공 여부 : " + isComplete.ToString());
                // }
                // catch (OperationCanceledException)
                // {
                //     Console.WriteLine("취소");
                // }
            }
        }

        // 단순 Task 실습
        private static async Task<string> ConnectAsync()
        {
            Console.WriteLine("ConnectAsync 시작");

            Console.WriteLine("1초 대기");
            await Task.Delay(TimeSpan.FromSeconds(1));

            Console.WriteLine("2초 대기");
            await Task.Delay(TimeSpan.FromSeconds(2));

            return "접속 완료";
        }
    }
}