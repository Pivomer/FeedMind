using FeedMind.Modules.Telegram.Application.Utils;
using FeedMind.Modules.Telegram.DTOs.Incoming;

namespace FeedMind.Modules.Telegram.Domain.Models;

public sealed class TelegramPost
{
    public int MessageId { get; private init; }
    public required string FormattedText { get; init; }
    public required string NormalizedText { get; init; }
    public long ChannelId { get; private init; }

    private static TelegramPost Create(int messageId, long channelId, string normalizedText)
    {
        var text = PostContentUtils.BuildFormattedPostText(normalizedText, channelId, messageId);
        return new TelegramPost
        {
            MessageId = messageId,
            ChannelId = channelId,
            FormattedText = text,
            NormalizedText = normalizedText
        };
    }

    public static TelegramPost? FromRaw(RawTelegramMessageDto dto)
    {
        var normalizedContent = PostContentUtils.NormalizeContent(dto.Text);
        if (normalizedContent is "")
        {
            return null;
        }
        return Create(
            messageId: dto.MessageId,
            channelId: dto.ChannelId,
            normalizedText: normalizedContent
        );
    }
}
