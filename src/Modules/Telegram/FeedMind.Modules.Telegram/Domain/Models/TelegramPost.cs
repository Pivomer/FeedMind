namespace FeedMind.Modules.Telegram.Domain.Models;

public sealed class TelegramPost
{
    public required string ChannelId { get; init; }
    public required string ChannelUsername { get; init; }
    public required int MessageId { get; init; }
    public required string Text { get; init; }
    public required DateTime Date { get; init; }
}
