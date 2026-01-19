using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FeedMind.API.BackgroundServices.Health;

public sealed class WorkersHealthCheck : IHealthCheck
{
    private readonly WorkerStates _states;

    public WorkersHealthCheck(WorkerStates states)
    {
        _states = states;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_states.FeedParser.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"FeedParser: {_states.FeedParser.Error}"));
        }

        if (!_states.BotPolling.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"BotPolling: {_states.BotPolling.Error}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All workers healthy"));
    }
}
