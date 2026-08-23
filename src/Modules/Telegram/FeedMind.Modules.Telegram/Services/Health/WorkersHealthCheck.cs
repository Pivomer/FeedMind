using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FeedMind.Modules.Telegram.Services.Health;

public sealed class WorkersHealthCheck : IHealthCheck
{
    private readonly WorkerStates _states;

    public WorkersHealthCheck(WorkerStates states)
    {
        _states = states;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var unhealthy = new Dictionary<string, object>();
        var degraded = new Dictionary<string, object>();

        Check("bot-polling", _states.BotPolling);

        if (unhealthy.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Some workers unhealthy", data: unhealthy));
        }

        if (degraded.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Some workers degraded", data: degraded));
        }
        return Task.FromResult(HealthCheckResult.Healthy("All workers healthy"));

        void Check(string name, WorkerState state)
        {
            switch (state.Status)
            {
                case WorkerHealthStatus.Unhealthy:
                    unhealthy[name] = new
                    {
                        error = state.Error ?? "unknown",
                        lastUpdated = state.LastUpdated
                    };
                    break;

                case WorkerHealthStatus.Degraded:
                    degraded[name] = new
                    {
                        error = state.Error ?? "performance issues",
                        lastUpdated = state.LastUpdated
                    };
                    break;
            }
        }
    }
}
