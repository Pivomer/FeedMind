using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Filtering.Application.Telegram;
using FeedMind.Modules.Filtering.Infrastructure.ServiceBus;
using FeedMind.Modules.Filtering.Services.Health;
using FeedMind.Modules.Filtering.Settings;
using FeedMind.Modules.Telegram.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static FeedMind.Modules.Filtering.Telemetry;

namespace FeedMind.Modules.Filtering.Services.Background;

public sealed class TelegramRequestConsumerService : ServiceBusConsumerBase<TelegramFilterRequest>
{
    private readonly TelegramFilteringHandler _handler;
    private readonly FilteringSettings _settings;

    public TelegramRequestConsumerService(
        ILogger<TelegramRequestConsumerService> logger,
        ServiceBusClient serviceBusClient,
        IOptions<FilteringSettings> options,
        TelegramFilteringHandler handler,
        WorkerStates states) : base(logger, serviceBusClient, options, states.TelegramFilterRequestConsumer)
    {
        _handler = handler;
        _settings = options.Value;
    }

    protected override string QueueName => _settings.TelegramRequestsQueueName;

    protected override async Task Handle(IReadOnlyList<MessageQueueItem<TelegramFilterRequest>> messages, CancellationToken cancellationToken)
    {
        foreach (var item in messages)
        {
            if(item.TryGetPayload() is not {} payload)
            {
                Logger.LogWarning("Received a message with empty payload. MessageId: {MessageId}", item.MessageId);
                await item.Complete(cancellationToken);
                continue;

            }

            var parentContext = item.GetTraceContext();

            using var activity = Source.StartActivity(Operations.TelegramRequestConsume, ActivityKind.Consumer, parentContext);
            activity?.SetTag(Tags.MessagingSystem, "servicebus");
            activity?.SetTag(Tags.MessagingOperation, "process");
            activity?.SetTag(Tags.MessagingMessageId, item.MessageId);
            activity?.SetTag(Tags.TelegramFilterChatId, payload.ChatId);
            activity?.SetTag(Tags.TelegramFilterChannelId, payload.ChannelId);

            try
            {
                await item.RenewLock(cancellationToken);
                await _handler.Handle(payload, cancellationToken);
                await item.Complete(cancellationToken);
                RecordSuccess();
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.AddException(exception);
                RecordError();
                Logger.LogError(exception, "Failed to process FilterRequest {MessageId}", item.MessageId);
                await item.Abandon(cancellationToken);
            }
        }
    }
}
