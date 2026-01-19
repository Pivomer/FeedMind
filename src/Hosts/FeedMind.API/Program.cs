using FeedMind.API.BackgroundServices;
using FeedMind.API.BackgroundServices.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace FeedMind.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<WorkerStates>();

        builder.Services.AddHostedService<TelegramFeedParserJob>();
        builder.Services.AddHostedService<TelegramBotPollingService>();

        builder.Services.AddHealthChecks().AddCheck<WorkersHealthCheck>("workers");

        var app = builder.Build();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");

        await app.RunAsync();
    }
}
