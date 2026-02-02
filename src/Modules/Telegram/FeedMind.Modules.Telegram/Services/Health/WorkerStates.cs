namespace FeedMind.Modules.Telegram.Services.Health;

public sealed class WorkerStates
{
    public WorkerState BotPolling { get; } = new();
    public WorkerState ChannelFeedListener { get; } = new();
    public WorkerState PostConsumer { get; } = new();
}
