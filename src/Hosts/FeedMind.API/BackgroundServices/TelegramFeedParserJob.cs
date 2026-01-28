using FeedMind.API.BackgroundServices.Health;
using FeedMind.API.Core.Exceptions;
using FeedMind.API.Settings;
using FeedMind.Modules.Telegram;
using Microsoft.Extensions.Options;

namespace FeedMind.API.BackgroundServices;

public sealed class TelegramFeedParserJob : BackgroundService
{
    private readonly ILogger<TelegramFeedParserJob> _logger;
    private readonly WorkerStates _states;
    private readonly Job _telegramJob;
    private readonly TimeSpan _workerDelay;

    public TelegramFeedParserJob(ILogger<TelegramFeedParserJob> logger, IOptions<AppSettings> options, WorkerStates states, Job telegramJob)
    {
        _logger = logger;
        _states = states;
        _telegramJob = telegramJob;
        _workerDelay = TimeSpan.FromMinutes(options.Value.FeedParserIntervalMin);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramFeedParserJob started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _telegramJob.Run(stoppingToken);
                _states.FeedParser.IsHealthy = true;
                _states.FeedParser.Error = null;
                await Task.Delay(_workerDelay, stoppingToken);
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
        }
    }
}
