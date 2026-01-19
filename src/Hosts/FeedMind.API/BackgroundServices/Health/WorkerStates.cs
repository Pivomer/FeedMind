namespace FeedMind.API.BackgroundServices.Health;

public sealed class WorkerStates
{
    public WorkerState FeedParser { get; } = new();
    public WorkerState BotPolling { get; } = new();
}
