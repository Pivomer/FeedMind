using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using static FeedMind.Modules.Filtering.Telemetry;

namespace FeedMind.Modules.Filtering.Infrastructure.ServiceBus;

public sealed class MessageQueueItem<TMessage>
{
    private readonly ServiceBusReceiver _receiver;
    private readonly ServiceBusReceivedMessage _message;

    public MessageQueueItem(ServiceBusReceiver receiver, ServiceBusReceivedMessage message)
    {
        _receiver = receiver;
        _message = message;
    }

    private string? GetProperty(string key) => _message.ApplicationProperties.TryGetValue(key, out var value) ? value?.ToString() : null;

    public TMessage? TryGetPayload()
    {
        try
        {
            return _message.Body.ToObjectFromJson<TMessage>();
        }
        catch
        {
            return default;
        }
    }

    public string MessageId => _message.MessageId;

    public async Task Complete(CancellationToken cancellationToken) => await _receiver.CompleteMessageAsync(_message, cancellationToken);

    public async Task Abandon(CancellationToken cancellationToken) => await _receiver.AbandonMessageAsync(_message, cancellationToken: cancellationToken);

    public async Task RenewLock(CancellationToken cancellationToken) => await _receiver.RenewMessageLockAsync(_message, cancellationToken);

    public async Task DeadLetter(string reason, string? description = null, CancellationToken cancellationToken = default) => await _receiver.DeadLetterMessageAsync(_message, reason, description, cancellationToken);

    public ActivityContext GetTraceContext()
    {
        ActivityContext.TryParse(
            GetProperty(TraceContext.TraceParent),
            GetProperty(TraceContext.TraceState),
            out var context);
        return context;
    }
}
