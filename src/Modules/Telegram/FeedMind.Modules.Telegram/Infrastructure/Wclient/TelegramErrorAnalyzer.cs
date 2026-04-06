using TL;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

public sealed record ErrorInfo(string Message);

public static class TelegramErrorAnalyzer
{
    public const int FloodWaitBufferSeconds = 5;

    public static ErrorInfo Analyze(RpcException exception)
    {
        return new ErrorInfo(Message: exception.Message);
    }

    public static int ParseFloodWaitSeconds(RpcException exception)
    {
        if (exception.X >= 0)
        {
            return exception.X;
        }

        throw new InvalidOperationException($"Failed to parse FLOOD_WAIT seconds from message: {exception.Message}");
    }
}
