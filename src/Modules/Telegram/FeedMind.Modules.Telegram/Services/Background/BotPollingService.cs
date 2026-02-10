using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Services.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class BotPollingService : BackgroundService
{
    private readonly ILogger<BotPollingService> _logger;
    private readonly BotApiClient _apiClient;
    private readonly WorkerState _health;

    public BotPollingService(ILogger<BotPollingService> logger, BotApiClient apiClient, WorkerStates states)
    {
        _logger = logger;
        _apiClient = apiClient;
        _health = states.BotPolling;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotPollingService started");
        try
        {
            _health.MarkHealthy();
            await _apiClient.StartPolling(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BotPollingService stopped gracefully");
        }
        catch (Exception exception)
        {
            _health.MarkUnhealthy(exception.Message);
            _logger.LogCritical(exception, "BotPollingService crashed");
            throw;
        }
    }
}
