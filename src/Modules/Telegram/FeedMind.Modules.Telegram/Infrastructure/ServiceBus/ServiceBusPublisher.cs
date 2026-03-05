using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Telegram.Contracts;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Infrastructure.ServiceBus;

public sealed class ServiceBusPublisher
{
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly ServiceBusSender _sender;

    public ServiceBusPublisher(ILogger<ServiceBusPublisher> logger, ServiceBusClient client, IOptions<TelegramSettings> options)
    {
        _logger = logger;
        _sender = client.CreateSender(options.Value.RequestsQueueName);
    }

    public async Task Publish(TelegramFilterRequest request, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(Operations.FilterRequestPublish, ActivityKind.Producer);
        activity?.SetTag(Tags.MessagingSystem, "servicebus");
        activity?.SetTag(Tags.MessagingOperation, "publish");
        activity?.SetTag(Tags.MessagingMessageId, request.MessageId);
        activity?.SetTag(Tags.TelegramChatId, request.ChatId);
        activity?.SetTag(Tags.TelegramChannelId, request.ChannelId);

        try
        {
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(request));
            if (activity != null)
            {
                message.ApplicationProperties[TraceContext.TraceParent] = activity.Id;
                message.ApplicationProperties[TraceContext.TraceState] = activity.TraceStateString ?? string.Empty;
            }

            await _sender.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            _logger.LogError(exception, "Failed to publish FilterRequest for message {MessageId}", request.MessageId);
            throw;
        }
    }
}
