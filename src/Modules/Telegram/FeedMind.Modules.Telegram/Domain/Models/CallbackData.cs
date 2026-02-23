namespace FeedMind.Modules.Telegram.Domain.Models;

public abstract record CallbackData
{
    public sealed record Feedback(int MessageId, MessageFeedback Feed) : CallbackData;
}
