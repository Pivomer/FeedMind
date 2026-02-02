using FeedMind.Modules.Telegram.Application.Utils;
using FeedMind.Modules.Telegram.DTOs.Incoming;

namespace FeedMind.Modules.Telegram.Domain.Models;

public sealed class TelegramPost
{
    public int MessageId { get; private init; }
    public required string Text { get; init; }
    public long ChannelId { get; private init; }

    private static TelegramPost Create(int messageId, long channelId, string? rawContent = null)
    {
        var text = PostContentUtils.BuildFormattedPostText(rawContent, channelId, messageId);
        return new TelegramPost
        {
            MessageId = messageId,
            ChannelId = channelId,
            Text = text
        };
    }

    public static TelegramPost FromRaw(RawTelegramMessageDto dto)
    {
        return Create(
            messageId: dto.MessageId,
            channelId: dto.ChannelId,
            rawContent: dto.Text
        );
    }
}
