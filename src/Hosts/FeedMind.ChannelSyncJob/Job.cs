using Microsoft.Extensions.Logging;

namespace FeedMind.ChannelSyncJob;

public sealed class Job
{
    private readonly ILogger<Job> _logger;

    public Job(ILogger<Job> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult> Run(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Job started");
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        _logger.LogInformation("Job finished");
        return ExecutionResult.Success();
    }
}