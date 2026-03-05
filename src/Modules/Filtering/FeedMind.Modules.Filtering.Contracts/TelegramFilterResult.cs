namespace FeedMind.Modules.Filtering.Contracts;

public sealed record TelegramFilterResult(
    string ChatId,
    long ChannelId,
    int MessageId,
    string Text,
    string Reason,
    bool ShouldShow
);
