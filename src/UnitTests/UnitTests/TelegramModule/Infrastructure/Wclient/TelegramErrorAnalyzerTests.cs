using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using TL;
using Xunit;

namespace UnitTests.TelegramModule.Infrastructure.Wclient;

public sealed class TelegramErrorAnalyzerTests
{
    [Theory]
    [InlineData("FLOOD_WAIT_86400", 86400)]
    [InlineData("FLOOD_WAIT_60", 60)]
    [InlineData("FLOOD_WAIT_1", 1)]
    public void ParseFloodWaitSeconds_ReturnsSeconds_ForValidMessage(string message, int expectedSeconds)
    {
        var exception = new RpcException(420, message, expectedSeconds);

        var result = TelegramErrorAnalyzer.ParseFloodWaitSeconds(exception);

        Assert.Equal(expectedSeconds, result);
    }

    [Theory]
    [InlineData("FLOOD_WAIT")]
    [InlineData("FLOOD_WAIT_")]
    [InlineData("FLOOD_WAIT_abc")]
    [InlineData("SOME_OTHER_ERROR")]
    public void ParseFloodWaitSeconds_Throws_ForInvalidMessage(string message)
    {
        var exception = new RpcException(420, message);

        Assert.Throws<InvalidOperationException>(() => TelegramErrorAnalyzer.ParseFloodWaitSeconds(exception));
    }
}
