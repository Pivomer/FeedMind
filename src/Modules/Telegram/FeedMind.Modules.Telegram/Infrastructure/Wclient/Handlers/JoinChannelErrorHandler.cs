using FeedMind.Modules.Telegram.Domain.Models;
using Microsoft.Extensions.Logging;
using TL;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient.Handlers;

public sealed class JoinChannelErrorHandler
{
    private readonly ILogger<JoinChannelErrorHandler> _logger;

    public JoinChannelErrorHandler(ILogger<JoinChannelErrorHandler> logger)
    {
        _logger = logger;
    }

    public JoinChannelInfo HandleRpcException(RpcException exception, string channelName, Action<TelegramTransientError>? onError)
    {
        var errorInfo = TelegramErrorAnalyzer.Analyze(exception);

        return errorInfo.Message switch
        {
            var msg when msg.Contains(TelegramErrorMessages.UserAlreadyParticipant) => new JoinChannelInfo.AlreadyJoined(),

            var msg when msg.Contains(TelegramErrorMessages.ChannelPrivate) => new JoinChannelInfo.AccessDenied(),

            var msg when msg.Contains(TelegramErrorMessages.ChannelInvalid) => new JoinChannelInfo.InvalidChannel(),

            var msg when msg.Contains(TelegramErrorMessages.ChannelsTooMuch) => new JoinChannelInfo.AccountLimitExceeded(),

            var msg when msg.Contains(TelegramErrorMessages.InviteRequestSent) => new JoinChannelInfo.InviteRequestSent(),

            var msg when msg.Contains(TelegramErrorMessages.FloodWait) => new JoinChannelInfo.FloodWait(TelegramErrorAnalyzer.ParseFloodWaitSeconds(exception)),

            _ => HandleGenericError(exception, channelName, onError)
        };
    }

    private JoinChannelInfo.TransientFailure HandleGenericError(RpcException exception, string channelName, Action<TelegramTransientError>? onError)
    {
        _logger.LogWarning(exception, "Unhandled Telegram join channel error for {Channel}", channelName);
        onError?.Invoke(new TelegramTransientError(exception));
        return new JoinChannelInfo.TransientFailure();
    }
}
