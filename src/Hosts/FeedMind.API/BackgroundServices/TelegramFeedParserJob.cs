using FeedMind.API.BackgroundServices.Health;
using FeedMind.API.Core.Exceptions;

namespace FeedMind.API.BackgroundServices;

public sealed class TelegramFeedParserJob : BackgroundService
{
    private readonly ILogger<TelegramFeedParserJob> _logger;
    private readonly WorkerStates _states;
    private readonly Random _random = new();

    public TelegramFeedParserJob(
        ILogger<TelegramFeedParserJob> logger,
        WorkerStates states)
    {
        _logger = logger;
        _states = states;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramFeedParserJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                _states.FeedParser.IsHealthy = true;
                _states.FeedParser.Error = null;
            }
            catch (TransientException ex)
            {
                _logger.LogWarning(ex, "FeedParser transient error");
                _states.FeedParser.IsHealthy = false;
                _states.FeedParser.Error = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "FeedParser fatal error, shutting down");
                _states.FeedParser.IsHealthy = false;
                _states.FeedParser.Error = ex.Message;
                throw;
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private Task DoWorkAsync(CancellationToken token)
    {
        if (_random.Next(0, 100) < 2)
            throw new Exception("Random fatal feed parser failure");

        if (_random.Next(0, 100) < 2)
            throw new TransientException("Random transient feed parser issue");

        return Task.CompletedTask;
    }
}
