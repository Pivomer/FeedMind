namespace FeedMind.API.BackgroundServices.Health;

public sealed class WorkerState
{
    public bool IsHealthy { get; set; } = true;
    public string? Error { get; set; }
}
