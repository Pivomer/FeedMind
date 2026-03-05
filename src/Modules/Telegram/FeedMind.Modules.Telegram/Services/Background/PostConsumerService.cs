using System.Diagnostics;
using System.Threading.Channels;
using FeedMind.Modules.Telegram.Application.Posts;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using FeedMind.Modules.Telegram.Mappings;
using FeedMind.Modules.Telegram.Services.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class PostConsumerService : BackgroundService
{
    private readonly ILogger<PostConsumerService> _logger;
    private readonly ChannelReader<RawTelegramMessageDto> _reader;
    private readonly TelegramPostDispatcher _postDispatcher;
    private readonly WorkerState _health;

    public PostConsumerService(ILogger<PostConsumerService> logger, ChannelReader<RawTelegramMessageDto> reader, TelegramPostDispatcher postDispatcher, WorkerStates states)
    {
        _logger = logger;
        _reader = reader;
        _postDispatcher = postDispatcher;
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
        if (string.IsNullOrEmpty(message.Text))
        {
            return;
        }

        using var activity = Source.StartActivity(Operations.InternalMessageProcess, ActivityKind.Consumer, message.TraceContext ?? default);
        try
        {
            activity?.SetTag(Tags.MessagingSystem, "internal-channel");
            activity?.SetTag(Tags.MessagingOperation, "process");
            activity?.SetTag(Tags.MessagingDestinationName, "raw-telegram-messages");
            activity?.SetTag(Tags.MessagingMessageId, message.MessageId.ToString());

            var postModel = TelegramMessageMapper.ToTelegramPost(message);
            if (postModel is null)
            {
                _logger.LogWarning("Failed to map raw message {MessageId} to TelegramPost", message.MessageId);
                return;
            }
            await _postDispatcher.Dispatch(postModel, stoppingToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }
}
