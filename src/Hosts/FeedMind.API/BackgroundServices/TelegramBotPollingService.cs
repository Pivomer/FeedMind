using FeedMind.API.BackgroundServices.Health;
using FeedMind.API.Core.Exceptions;

namespace FeedMind.API.BackgroundServices;

public sealed class TelegramBotPollingService : BackgroundService
{
    private readonly ILogger<TelegramBotPollingService> _logger;
    private readonly WorkerStates _states;
    private readonly Random _random = new();

    public TelegramBotPollingService(ILogger<TelegramBotPollingService> logger, WorkerStates states)
    {
        _logger = logger;
        _states = states;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramBotPollingService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);

                _states.BotPolling.IsHealthy = true;
                _states.BotPolling.Error = null;
            }
            catch (TransientException ex)
            {
                _logger.LogWarning(ex, "BotPolling transient error");
                _states.BotPolling.IsHealthy = false;
                _states.BotPolling.Error = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "BotPolling fatal error, shutting down");
                _states.BotPolling.IsHealthy = false;
                _states.BotPolling.Error = ex.Message;
                throw;
            }

            await Task.Delay(1500, stoppingToken);
        }
    }

    private Task DoWorkAsync(CancellationToken token)
    {
        if (_random.Next(0, 100) < 2)
            throw new Exception("Random fatal bot polling failure");

        if (_random.Next(0, 100) < 2)
            throw new TransientException("Random transient bot polling issue");

        return Task.CompletedTask;
    }
}
