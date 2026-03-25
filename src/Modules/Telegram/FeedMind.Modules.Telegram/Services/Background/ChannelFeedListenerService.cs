using System.Diagnostics;
using System.Threading.Channels;
using FeedMind.Modules.Telegram.Application.Channels;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using FeedMind.Modules.Telegram.Mappings;
using FeedMind.Modules.Telegram.Services.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class ChannelFeedListenerService : BackgroundService
{
    private readonly ILogger<ChannelFeedListenerService> _logger;
    private readonly WorkerState _health;
    private readonly WTelegramClient _telegramClient;
    private readonly TelegramMessageMapper _mapper;
    private readonly ChannelWriter<RawTelegramMessageDto> _channelWriter;
    private readonly ChannelSubscriptionManager _channelSubscriptionManager;

    private Action<TelegramTransientError>? _transientErrorHandler;
    private Action<TelegramFatalError>? _fatalErrorHandler;

    public ChannelFeedListenerService(
        ILogger<ChannelFeedListenerService> logger,
        WTelegramClient telegramClient,
        TelegramMessageMapper mapper,
        ChannelWriter<RawTelegramMessageDto> channelWriter,
        ChannelSubscriptionManager channelSubscriptionManager,
        WorkerStates states)
    {
        _logger = logger;
        _telegramClient = telegramClient;
        _mapper = mapper;
        _channelWriter = channelWriter;
        _channelSubscriptionManager = channelSubscriptionManager;
        _health = states.ChannelFeedListener;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChannelFeedListenerService started");
        try
        {
            RegisterHealthHooks();
            await _channelSubscriptionManager.InitializeListeningChannels(stoppingToken);
            await _channelSubscriptionManager.Sync(stoppingToken);
            await StartListener();
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ChannelFeedListenerService stopped gracefully");
        }
        catch (Exception exception)
        {
            _health.MarkUnhealthy(exception.Message);
            _logger.LogCritical(exception, "ChannelFeedListenerService crashed");
            throw;
        }
    }

    private void RegisterHealthHooks()
    {
        _transientErrorHandler = error =>
        {
            _logger.LogWarning(error.Exception, "Transient Telegram error");
            _health.RecordError();
        };

        _fatalErrorHandler = error =>
        {
            _logger.LogError(error.Exception, "Fatal Telegram error");
            _health.MarkUnhealthy(error.Exception.Message);
        };

        _telegramClient.OnTransientError += _transientErrorHandler;
        _telegramClient.OnFatalError += _fatalErrorHandler;
    }

    private async Task StartListener()
    {
        _telegramClient.OnMessageReceived += MessageReceived;
        await _telegramClient.SubscribeToUpdates();
        _health.MarkHealthy();
    }

    private async Task MessageReceived(TL.Message message)
    {
        using var activity = Source.StartActivity(Operations.TelegramMessageReceive, ActivityKind.Consumer);
        try
        {
            activity?.SetTag(Tags.MessagingSystem, "telegram");
            activity?.SetTag(Tags.MessagingOperation, "receive");
            activity?.SetTag(Tags.MessagingDestinationName, "telegram-channel");
            activity?.SetTag(Tags.TelegramChannelId, message.peer_id?.ToString() ?? "unknown");
            activity?.SetTag(Tags.MessagingMessageId, message.id.ToString());

            var dto = _mapper.ToRawMessageDto(message);
            dto.TraceContext = activity?.Context;
            await _channelWriter.WriteAsync(dto);
            _health.RecordSuccess();
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);

            _logger.LogError(exception, "Message processing failed");
            _health.RecordError();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ChannelFeedListenerService stopping");

        _telegramClient.OnMessageReceived -= MessageReceived;
        _telegramClient.OnTransientError -= _transientErrorHandler;
        _telegramClient.OnFatalError -= _fatalErrorHandler;

        await _telegramClient.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
