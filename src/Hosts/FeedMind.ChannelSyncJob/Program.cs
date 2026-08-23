using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FeedMind.ChannelSyncJob;

public static class Program
{
    private const string ServiceName = "FeedMind.ChannelSyncJob";

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"Starting: {ServiceName}");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<Job>();

        using var host = builder.Build();

        await host.StartAsync();
        var job = host.Services.GetRequiredService<Job>();
        ExecutionResult result = await job.Run(CancellationToken.None);
        await host.StopAsync();

        return result.ExitCode;
    }
}