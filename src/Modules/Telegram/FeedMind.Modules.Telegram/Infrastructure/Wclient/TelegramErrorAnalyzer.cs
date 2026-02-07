using TL;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

public sealed record ErrorInfo(string Message);

public static class TelegramErrorAnalyzer
{
    public static ErrorInfo Analyze(RpcException ex)
    {
        return new ErrorInfo(Message: ex.Message);
    }
}
