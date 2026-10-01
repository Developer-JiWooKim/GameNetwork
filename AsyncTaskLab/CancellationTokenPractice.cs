namespace AysncTask
{
    public sealed class CancellationTokenPractice
    {
        public async Task WaitForConnectionAsync(CancellationToken token)
        {
            int count = 0;
            while (!token.IsCancellationRequested && count < 3)
            {
                Console.WriteLine("접속 요청 중...");
                await Task.Delay(TimeSpan.FromSeconds(1), token);
                count++;
            }
        }
    }
}