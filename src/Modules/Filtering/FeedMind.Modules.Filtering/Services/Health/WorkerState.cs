namespace FeedMind.Modules.Filtering.Services.Health;

public sealed class WorkerState
{
    private int _consecutiveErrors;
    private int _consecutiveSuccesses;
    private const int ErrorThreshold = 3;
    private const int RecoveryThreshold = 5;

    public WorkerHealthStatus Status { get; private set; } = WorkerHealthStatus.Unhealthy;
    public string? Error { get; private set; }
    public DateTimeOffset LastUpdated { get; private set; }

    public void MarkHealthy()
    {
        Update(WorkerHealthStatus.Healthy, null);
        _consecutiveErrors = 0;
        _consecutiveSuccesses = 0;
    }

    public void MarkUnhealthy(string error)
    {
        Update(WorkerHealthStatus.Unhealthy, error);
        _consecutiveSuccesses = 0;
    }

    public void MarkDegraded(string error)
    {
        Update(WorkerHealthStatus.Degraded, error);
        _consecutiveSuccesses = 0;
    }

    public void RecordSuccess()
    {
        _consecutiveErrors = 0;
        _consecutiveSuccesses++;

        if (Status == WorkerHealthStatus.Degraded && _consecutiveSuccesses >= RecoveryThreshold)
        {
            MarkHealthy();
        }
    }

    public void RecordError()
    {
        _consecutiveSuccesses = 0;
        _consecutiveErrors++;

        var errorMsg = $"{_consecutiveErrors} consecutive errors";
        if (Status == WorkerHealthStatus.Healthy && _consecutiveErrors >= ErrorThreshold)
        {
            MarkDegraded(errorMsg);
        }
        else if (Status == WorkerHealthStatus.Degraded)
        {
            MarkUnhealthy(errorMsg);
        }
    }

    private void Update(WorkerHealthStatus status, string? error)
    {
        Status = status;
        Error = error;
        LastUpdated = DateTimeOffset.UtcNow;
    }
}
