using System.Diagnostics;
using FeedMind.Modules.Filtering.Contracts;
using FeedMind.Modules.Filtering.Infrastructure.ServiceBus;
using FeedMind.Modules.Telegram.Contracts;
using static FeedMind.Modules.Filtering.Telemetry;

namespace FeedMind.Modules.Filtering.Application.Handlers;

public sealed class TelegramFilteringHandler
{
    private readonly TelegramResultPublisher _publisher;

    public TelegramFilteringHandler(TelegramResultPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task Handle(TelegramFilterRequest request, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(Operations.TelegramRequestProcess);
        activity?.SetTag(Tags.TelegramFilterChatId, request.ChatId);
        activity?.SetTag(Tags.TelegramFilterChannelId, request.ChannelId);
        activity?.SetTag(Tags.MessagingMessageId, request.MessageId);

        var result = new TelegramFilterResult(
            ChatId: request.ChatId,
            ChannelId: request.ChannelId,
            MessageId: request.MessageId,
            Text: request.Text,
            ShouldShow: true,
            Reason: "stub");

        activity?.SetTag(Tags.TelegramFilterDecision, result.ShouldShow ? "show" : "hide");

        try
        {
            await _publisher.Publish(result, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
