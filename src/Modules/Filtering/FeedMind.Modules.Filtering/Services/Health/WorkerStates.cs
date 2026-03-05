namespace FeedMind.Modules.Filtering.Services.Health;

public sealed class WorkerStates
{
    public WorkerState TelegramFilterRequestConsumer { get; } = new();
}
