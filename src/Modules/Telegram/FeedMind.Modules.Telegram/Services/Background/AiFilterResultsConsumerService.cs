using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Telegram.Application.Posts;
using FeedMind.Modules.Telegram.Infrastructure.ServiceBus;
using FeedMind.Modules.Telegram.Services.Health;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class AiFilterResultsConsumerService : BackgroundService
{
    private readonly ILogger<AiFilterResultsConsumerService> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly TelegramPostDispatcher _postDispatcher;
    private readonly WorkerState _health;
    private readonly TelegramSettings _settings;
    private readonly TimeSpan _longPollingTimeSpan;
    private const int ServiceBusReceiveMaxSize = 100;

    public AiFilterResultsConsumerService(
        ILogger<AiFilterResultsConsumerService> logger,
        TelegramPostDispatcher postDispatcher,
        ServiceBusClient serviceBusClient,
        IOptions<TelegramSettings> options,
        WorkerStates states)
    {
        _logger = logger;
        _postDispatcher = postDispatcher;
        _serviceBusClient = serviceBusClient;
        _settings = options.Value;
        _longPollingTimeSpan = _settings.LongPollingTimeSpan;
        _health = states.AiFilterResultsConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiFilterResultsConsumerService starting");
        try
        {
            var receiver = _serviceBusClient.CreateReceiver(_settings.ResultsQueueName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
            _logger.LogInformation("AiFilterResultsConsumerService started, listening on {QueueName}", _settings.ResultsQueueName);

            _health.MarkHealthy();
            while (!stoppingToken.IsCancellationRequested)
            {
                var receivedMessages = await receiver.ReceiveMessagesAsync(ServiceBusReceiveMaxSize, _longPollingTimeSpan, stoppingToken);
                if (!receivedMessages.Any())
                {
                    continue;
                }

                MessageQueueItem[] messages = receivedMessages.Select(x => new MessageQueueItem(receiver, x)).ToArray();
                try
                {
                    await ProcessMessages(messages, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing batch");
                    foreach (var item in messages)
                    {
                        await item.DeadLetter("Unhandled exception", ex.Message, stoppingToken);
                    }
                }

            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AiFilterResultsConsumerService stopped gracefully");
        }
        catch (Exception exception)
        {
            _health.MarkUnhealthy(exception.Message);
            _logger.LogCritical(exception, "AiFilterResultsConsumerService crashed");
            throw;
        }
    }

    private async Task ProcessMessages(MessageQueueItem[] messages, CancellationToken stoppingToken)
    {
        foreach (var message in messages)
        {
            if (message.TryGetPayload() is not { } payload)
            {
                _logger.LogWarning("Received a message with empty payload. MessageId: {MessageId}", message.MessageId);
                await message.Complete(stoppingToken);
                continue;
            }

            var parentContext = message.GetTraceContext();

            using var activity = Source.StartActivity(Operations.FilterResultProcess, ActivityKind.Consumer, parentContext);
            activity?.SetTag(Tags.MessagingSystem, "servicebus");
            activity?.SetTag(Tags.MessagingMessageId, message.MessageId);
            activity?.SetTag(Tags.TelegramChatId, payload.ChatId);

            try
            {
                await message.RenewLock(stoppingToken);
                await _postDispatcher.Deliver(payload, stoppingToken);
                await message.Complete(stoppingToken);
                _health.RecordSuccess();
            }
            catch (Exception exception)
            {
                _health.RecordError();
                _logger.LogError(exception, "Failed to process filter result {MessageId}", message.MessageId);
                await message.Abandon(stoppingToken);
            }
        }
    }
}
