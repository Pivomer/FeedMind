using System.Threading.Channels;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using FeedMind.Modules.Telegram.Mappings;
using FeedMind.Modules.Telegram.Services.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class ChannelFeedListenerService : BackgroundService
{
    private readonly ILogger<ChannelFeedListenerService> _logger;
    private readonly WorkerState _health;
    private readonly WTelegramClient _telegramClient;
    private readonly TelegramMessageMapper _mapper;
    private readonly ChannelWriter<RawTelegramMessageDto> _channelWriter;

    private Action<TelegramTransientError>? _transientErrorHandler;
    private Action<TelegramFatalError>? _fatalErrorHandler;

    public ChannelFeedListenerService(
        ILogger<ChannelFeedListenerService> logger,
        WTelegramClient telegramClient,
        TelegramMessageMapper mapper,
        ChannelWriter<RawTelegramMessageDto> channelWriter,
        WorkerStates states)
    {
        _logger = logger;
        _telegramClient = telegramClient;
        _mapper = mapper;
        _channelWriter = channelWriter;
        _health = states.ChannelFeedListener;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChannelFeedListenerService started");
        try
        {
            RegisterHealthHooks();
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
        try
        {
            var dto = _mapper.ToRawMessageDto(message);
            await _channelWriter.WriteAsync(dto);
            _health.RecordSuccess();
        }
        catch (Exception exception)
        {
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

        await base.StopAsync(cancellationToken);
    }
}
