namespace FeedMind.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Container started successfully!");
        while (true)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Still running...");
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }
}
