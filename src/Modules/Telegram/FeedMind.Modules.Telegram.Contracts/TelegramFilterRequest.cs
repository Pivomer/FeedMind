namespace FeedMind.Modules.Telegram.Contracts;

public sealed record TelegramFilterRequest(
    string ChatId,
    long ChannelId,
    int MessageId,
    string Text,
    IReadOnlyList<string> LikedTexts,
    IReadOnlyList<string> DislikedTexts
);
