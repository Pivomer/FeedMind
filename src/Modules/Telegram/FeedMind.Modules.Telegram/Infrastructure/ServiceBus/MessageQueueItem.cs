using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Filtering.Contracts;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Infrastructure.ServiceBus;

public sealed class MessageQueueItem
{
    private readonly ServiceBusReceiver _receiver;
    private readonly ServiceBusReceivedMessage _message;

    public MessageQueueItem(ServiceBusReceiver receiver, ServiceBusReceivedMessage message)
    {
        _receiver = receiver;
        _message = message;
    }

    private string? GetProperty(string key) => _message.ApplicationProperties.TryGetValue(key, out var value) ? value?.ToString() : null;

    public string MessageId => _message.MessageId;

    public async Task Complete(CancellationToken cancellationToken) => await _receiver.CompleteMessageAsync(_message, cancellationToken);

    public async Task Abandon(CancellationToken cancellationToken) => await _receiver.AbandonMessageAsync(_message, cancellationToken: cancellationToken);

    public async Task DeadLetter(string reason, string? description = null, CancellationToken cancellationToken = default) => await _receiver.DeadLetterMessageAsync(_message, reason, description, cancellationToken);

    public TelegramFilterResult? TryGetPayload()
    {
        try
        {
            return _message.Body.ToObjectFromJson<TelegramFilterResult>();
        }
        catch
        {
            return null;
        }
    }

    public ActivityContext GetTraceContext()
    {
        ActivityContext.TryParse(
            GetProperty(TraceContext.TraceParent),
            GetProperty(TraceContext.TraceState),
            out var context);
        return context;
    }
}
