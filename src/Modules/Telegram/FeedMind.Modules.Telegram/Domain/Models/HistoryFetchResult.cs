namespace FeedMind.Modules.Telegram.Domain.Models;

public abstract record HistoryFetchResult
{
    public sealed record Success(IReadOnlyList<TL.Message> Messages) : HistoryFetchResult;

    public sealed record FloodWait(int WaitSeconds) : HistoryFetchResult;

    public sealed record TransientFailure : HistoryFetchResult;
}
