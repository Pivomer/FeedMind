using FeedMind.Modules.Telegram.Contracts;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

namespace FeedMind.Modules.Telegram.Application.Filtering;

public sealed class FilterRequestBuilder
{
    private readonly MessageRepository _messages;
    private const int MaxFeedbackItems = 15;

    public FilterRequestBuilder(MessageRepository messages)
    {
        _messages = messages;
    }

    public async Task<TelegramFilterRequest> Build(string chatId, long channelId, int messageId, string text, CancellationToken ct)
    {
        var liked = await _messages.GetByFeedback(chatId, channelId, feedback: 1, take: MaxFeedbackItems, ct);
        var disliked = await _messages.GetByFeedback(chatId, channelId, feedback: -1, take: MaxFeedbackItems, ct);

        return new TelegramFilterRequest(
            ChatId: chatId,
            ChannelId: channelId,
            MessageId: messageId,
            Text: text,
            LikedTexts: liked,
            DislikedTexts: disliked);
    }
}
