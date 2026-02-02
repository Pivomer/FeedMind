using FeedMind.Modules.Telegram.Services.Health;
using Xunit;

namespace UnitTests.TelegramModule.Services.Health;

public sealed class WorkerStateTests
{
    [Fact]
    public void Constructor_SetsInitialStateAsUnhealthy()
    {
        var state = new WorkerState();

        Assert.Equal(WorkerHealthStatus.Unhealthy, state.Status);
        Assert.Null(state.Error);
    }

    [Fact]
    public void MarkHealthy_ResetsStateAndClearsError()
    {
        var state = new WorkerState();

        state.MarkHealthy();

        Assert.Equal(WorkerHealthStatus.Healthy, state.Status);
        Assert.Null(state.Error);
    }

    [Fact]
    public void MarkDegraded_SetsDegradedStateWithError()
    {
        var state = new WorkerState();
        var error = "degraded";

        state.MarkDegraded(error);

        Assert.Equal(WorkerHealthStatus.Degraded, state.Status);
        Assert.Equal(error, state.Error);
    }

    [Fact]
    public void MarkUnhealthy_SetsUnhealthyStateWithError()
    {
        var state = new WorkerState();
        var error = "fatal";

        state.MarkUnhealthy(error);

        Assert.Equal(WorkerHealthStatus.Unhealthy, state.Status);
        Assert.Equal(error, state.Error);
    }

    [Fact]
    public void RecordError_WhenHealthyAndThresholdReached_MovesToDegraded()
    {
        var state = new WorkerState();
        state.MarkHealthy();

        state.RecordError();
        state.RecordError();
        state.RecordError();

        Assert.Equal(WorkerHealthStatus.Degraded, state.Status);
        Assert.Equal("3 consecutive errors", state.Error);
    }

    [Fact]
    public void RecordError_WhenDegraded_MovesToUnhealthy()
    {
        var state = new WorkerState();
        state.MarkHealthy();

        state.RecordError();
        state.RecordError();
        state.RecordError();
        state.RecordError();

        Assert.Equal(WorkerHealthStatus.Unhealthy, state.Status);
        Assert.Equal("4 consecutive errors", state.Error);
    }

    [Fact]
    public void RecordError_WhenAlreadyUnhealthy_DoesNotChangeState()
    {
        var state = new WorkerState();

        state.RecordError();
        state.RecordError();
        state.RecordError();

        Assert.Equal(WorkerHealthStatus.Unhealthy, state.Status);
    }

    [Fact]
    public void RecordSuccess_WhenDegradedAndRecoveryThresholdReached_RecoversToHealthy()
    {
        var state = new WorkerState();
        state.MarkHealthy();

        state.RecordError();
        state.RecordError();
        state.RecordError();

        state.RecordSuccess();
        state.RecordSuccess();
        state.RecordSuccess();
        state.RecordSuccess();
        state.RecordSuccess();

        Assert.Equal(WorkerHealthStatus.Healthy, state.Status);
        Assert.Null(state.Error);
    }

    [Fact]
    public void RecordSuccess_AfterErrors_ResetsErrorCounter()
    {
        var state = new WorkerState();
        state.MarkHealthy();

        state.RecordError();
        state.RecordError();
        state.RecordSuccess();
        state.RecordError();
        state.RecordError();
        state.RecordError();

        Assert.Equal(WorkerHealthStatus.Degraded, state.Status);
        Assert.Equal("3 consecutive errors", state.Error);
    }

    [Fact]
    public void StateChange_UpdatesLastUpdatedTimestamp()
    {
        var state = new WorkerState();
        var before = DateTimeOffset.UtcNow;

        state.MarkHealthy();

        Assert.True(state.LastUpdated >= before);
    }
}
