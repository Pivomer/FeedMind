namespace FeedMind.ChannelSyncJob;

public sealed class ExecutionResult
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    private readonly bool _isSuccess;
    public int ExitCode => _isSuccess ? SuccessExitCode : FailureExitCode;

    private ExecutionResult(bool isSuccess)
    {
        _isSuccess = isSuccess;
    }

    public static ExecutionResult Success()
    {
        return new ExecutionResult(true);
    }

    public static ExecutionResult Failure()
    {
        return new ExecutionResult(false);
    }
}