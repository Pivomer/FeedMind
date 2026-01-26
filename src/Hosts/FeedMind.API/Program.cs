using FeedMind.API.BackgroundServices;
using FeedMind.API.BackgroundServices.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace FeedMind.API;

public static class Program
{
    private const string ServiceName = "FeedMind.API";
    private const string AppHome = "APPLICATION_HOME";
    private const string ApplicationEnvironment = "APPLICATION_ENVIRONMENT";

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Starting: {ServiceName}");
        string? appHome = Environment.GetEnvironmentVariable(AppHome);
        string? appEnvironment = Environment.GetEnvironmentVariable(ApplicationEnvironment);

        if (appHome is null)
        {
            throw new ArgumentNullException(nameof(appHome), $"{AppHome} is required");
        }

        if (appEnvironment is null)
        {
            throw new ArgumentNullException(nameof(appEnvironment), $"{ApplicationEnvironment} is required");
        }

        var configPath = Path.Combine(appHome, "appsettings.json");

        Console.WriteLine($"Starting: {AppHome}: {appHome} ConfigDirectory: {configPath} {ApplicationEnvironment}: {appEnvironment}");

        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();

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
