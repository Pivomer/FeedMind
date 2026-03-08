using System.Diagnostics;
using FeedMind.Modules.Filtering.Contracts;
using FeedMind.Modules.Filtering.Infrastructure.ServiceBus;
using FeedMind.Modules.Telegram.Contracts;
using static FeedMind.Modules.Filtering.Telemetry;

namespace FeedMind.Modules.Filtering.Application.Telegram;

public sealed class TelegramFilteringHandler
{
    private readonly TelegramResultPublisher _publisher;
    private readonly OpenAiFilterClient _filterClient;

    public TelegramFilteringHandler(TelegramResultPublisher publisher, OpenAiFilterClient filterClient)
    {
        _publisher = publisher;
        _filterClient = filterClient;
    }

    public async Task Handle(TelegramFilterRequest request, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(Operations.TelegramRequestProcess);
        activity?.SetTag(Tags.TelegramFilterChatId, request.ChatId);
        activity?.SetTag(Tags.TelegramFilterChannelId, request.ChannelId);
        activity?.SetTag(Tags.MessagingMessageId, request.MessageId);

        var filterResult = await Filter(request, cancellationToken);

        activity?.SetTag(Tags.TelegramFilterDecision, filterResult.ShouldShow ? "show" : "hide");
        activity?.SetTag(Tags.TelegramFilterReason, filterResult.Reason);

        var result = new TelegramFilterResult(
            ChatId: request.ChatId,
            ChannelId: request.ChannelId,
            MessageId: request.MessageId,
            Text: request.Text,
            ShouldShow: filterResult.ShouldShow,
            Reason: filterResult.Reason);

        activity?.SetTag(Tags.TelegramFilterDecision, result.ShouldShow ? "show" : "hide");

        try
        {
            await _publisher.Publish(result, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }

    private async Task<FilterResult> Filter(TelegramFilterRequest request, CancellationToken cancellationToken)
    {
        if (request.LikedTexts.Count == 0 && request.DislikedTexts.Count == 0)
        {
            return new FilterResult(true, "no-history");
        }

        var userPrompt = TelegramFilterPromptBuilder.Build(request);
        return await _filterClient.Filter(userPrompt, cancellationToken);
    }
}
