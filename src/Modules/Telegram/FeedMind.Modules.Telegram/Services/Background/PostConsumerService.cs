using System.Threading.Channels;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Mappings;
using FeedMind.Modules.Telegram.Services.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class PostConsumerService : BackgroundService
{
    private readonly ILogger<PostConsumerService> _logger;
    private readonly ChannelReader<RawTelegramMessageDto> _reader;
    private readonly BotApiClient _botApiClient;
    private readonly WorkerState _health;

    public PostConsumerService(ILogger<PostConsumerService> logger, ChannelReader<RawTelegramMessageDto> reader, WorkerStates states, BotApiClient botApiClient)
    {
        _logger = logger;
        _reader = reader;
        _botApiClient = botApiClient;
        _health = states.PostConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PostConsumerService started");
        try
        {
            _health.MarkHealthy();

            await foreach (var message in _reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessMessage(message, stoppingToken);
                    _health.RecordSuccess();
                }
                catch (Exception exception)
                {
                    _health.RecordError();
                    _logger.LogError(exception, "Post processing failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PostConsumerService stopped gracefully");
        }
        catch (Exception exception)
        {
            _health.MarkUnhealthy(exception.Message);
            _logger.LogCritical(exception, "PostConsumerService crashed");
            throw;
        }
    }

    private async Task ProcessMessage(RawTelegramMessageDto message, CancellationToken stoppingToken)
    {
        var postModel = TelegramMessageMapper.ToTelegramPost(message);
        await _botApiClient.SendPostToChats(postModel, stoppingToken);
    }
}
