using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using static FeedMind.Modules.Filtering.Telemetry;

namespace FeedMind.Modules.Filtering.Infrastructure.ServiceBus;

public abstract class ServiceBusPublisherBase<TMessage>
{
    private ILogger Logger { get; }
    private readonly ServiceBusSender _sender;
    protected abstract string OperationName { get; }

    protected ServiceBusPublisherBase(ILogger logger, ServiceBusClient client, string queueName)
    {
        Logger = logger;
        _sender = client.CreateSender(queueName);
    }

    public async Task Publish(TMessage message, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(OperationName, ActivityKind.Producer);
        activity?.SetTag(Tags.MessagingSystem, "servicebus");
        activity?.SetTag(Tags.MessagingOperation, "publish");
        activity?.SetTag(Tags.MessagingDestinationName, _sender.EntityPath);

        try
        {
            var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message));
            if (activity != null)
            {
                sbMessage.ApplicationProperties[TraceContext.TraceParent] = activity.Id;
                sbMessage.ApplicationProperties[TraceContext.TraceState] = activity.TraceStateString ?? string.Empty;
            }

            await _sender.SendMessageAsync(sbMessage, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            Logger.LogError(exception, "Failed to publish message of type {MessageType}", typeof(TMessage).Name);
            throw;
        }
    }
}
