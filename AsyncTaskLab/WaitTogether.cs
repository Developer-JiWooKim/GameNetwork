namespace AysncTask
{
    public sealed class WaitTogether
    {
        public async Task<bool> LoadProfileAsync(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            Console.WriteLine("프로필 로드 완료");

            return true;
        }

        public async Task<bool> LoadInventoryAsync(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            Console.WriteLine("인벤토리 로드 완료");

            return true;
        }
    }
}