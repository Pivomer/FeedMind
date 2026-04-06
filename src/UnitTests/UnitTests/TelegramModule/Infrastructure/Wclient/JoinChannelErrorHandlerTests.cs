using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using FeedMind.Modules.Telegram.Infrastructure.Wclient.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using TL;
using Xunit;

namespace UnitTests.TelegramModule.Infrastructure.Wclient;

public sealed class JoinChannelErrorHandlerTests
{
    private readonly JoinChannelErrorHandler _handler = new(NullLogger<JoinChannelErrorHandler>.Instance);

    [Fact]
    public void HandleRpcException_ReturnsRateLimited_ForFloodWait()
    {
        var exception = new RpcException(420, "FLOOD_WAIT_86400", 86400);

        var result = _handler.HandleRpcException(exception, "test_channel", null);

        var rateLimited = Assert.IsType<JoinChannelInfo.FloodWait>(result);
        Assert.Equal(86400, rateLimited.WaitSeconds);
    }

    [Fact]
    public void HandleRpcException_ReturnsAlreadyJoined_ForUserAlreadyParticipant()
    {
        var exception = new RpcException(400, "USER_ALREADY_PARTICIPANT");

        var result = _handler.HandleRpcException(exception, "test_channel", null);

        Assert.IsType<JoinChannelInfo.AlreadyJoined>(result);
    }

    [Fact]
    public void HandleRpcException_ReturnsAccessDenied_ForChannelPrivate()
    {
        var exception = new RpcException(400, "CHANNEL_PRIVATE");

        var result = _handler.HandleRpcException(exception, "test_channel", null);

        Assert.IsType<JoinChannelInfo.AccessDenied>(result);
    }

    [Fact]
    public void HandleRpcException_ReturnsTransientFailure_ForUnknownError()
    {
        var exception = new RpcException(400, "SOME_UNKNOWN_ERROR");

        var result = _handler.HandleRpcException(exception, "test_channel", null);

        Assert.IsType<JoinChannelInfo.TransientFailure>(result);
    }

    [Fact]
    public void HandleRpcException_InvokesOnError_ForUnknownError()
    {
        var exception = new RpcException(400, "SOME_UNKNOWN_ERROR");
        TelegramTransientError? capturedError = null;

        _handler.HandleRpcException(exception, "test_channel", error => capturedError = error);

        Assert.NotNull(capturedError);
        Assert.Equal(exception, capturedError.Exception);
    }
}
