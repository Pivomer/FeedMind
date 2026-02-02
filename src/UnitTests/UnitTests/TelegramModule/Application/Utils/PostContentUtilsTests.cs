using FeedMind.Modules.Telegram.Application.Utils;
using Xunit;

namespace UnitTests.TelegramModule.Application.Utils;

public sealed class PostContentUtilsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("short text", "short text")]
    [InlineData("  hello world  ", "hello world")]
    public void NormalizeContent_TrimsAndHandlesEmpty(string? input, string expected)
    {
        var result = PostContentUtils.NormalizeContent(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(799, 799, false)]
    [InlineData(800, 800, false)]
    [InlineData(801, 800, true)]
    [InlineData(1200, 800, true)]
    public void NormalizeContent_RespectsMaxLength(int inputLength, int expectedLength, bool shouldHaveEllipsis)
    {
        var input = new string('a', inputLength);
        var result = PostContentUtils.NormalizeContent(input);

        Assert.Equal(expectedLength + (shouldHaveEllipsis ? 1 : 0), result.Length);

        if (shouldHaveEllipsis)
        {
            Assert.EndsWith("…", result);
        }
        else
        {
            Assert.DoesNotContain("…", result);
        }
    }

    [Fact]
    public void NormalizeContent_CutsAtLastSuitableCharacterWhenPossible()
    {
        var input = new string('a', 798) + " b" + new string('c', 6);
        var result = PostContentUtils.NormalizeContent(input);

        Assert.Equal(799, result.Length);
        Assert.EndsWith("…", result);
        Assert.Contains("a…", result);
    }

    [Theory]
    [InlineData(-1001234567890L, 42, "https://t.me/c/1234567890/42")]
    [InlineData(1234567890L, 100, "https://t.me/c/1234567890/100")]
    [InlineData(-1000000000001L, 5, "https://t.me/c/1/5")]
    [InlineData(-999999999999L, 777, "https://t.me/c/999999999999/777")]
    public void BuildFormattedPostText_GeneratesCorrectChannelUrl(long channelId, int messageId, string expectedUrlPart)
    {
        var result = PostContentUtils.BuildFormattedPostText("test content", channelId, messageId, "Open");

        Assert.StartsWith("test content\n", result);
        Assert.Contains($"<a href=\"{expectedUrlPart}\">Open</a>", result);
    }

    [Fact]
    public void BuildFormattedPostText_UsesDefaultLinkText()
    {
        var result = PostContentUtils.BuildFormattedPostText("hello", -1009876543210L, 123);

        Assert.StartsWith("hello\n", result);
        Assert.EndsWith("<a href=\"https://t.me/c/9876543210/123\">Link</a>", result);
    }

    [Theory]
    [InlineData(null, -1001112223334L, 1, "")]
    [InlineData("", -1001112223334L, 1, "")]
    [InlineData("   trimmed   ", -1001112223334L, 1, "trimmed")]
    public void BuildFormattedPostText_HandlesEmptyOrWhitespaceContent(
        string? content,
        long channelId,
        int messageId,
        string expectedStart)
    {
        var result = PostContentUtils.BuildFormattedPostText(content, channelId, messageId);

        Assert.StartsWith(expectedStart, result.TrimStart());
        Assert.Contains("<a href=\"https://t.me/c/", result);
        Assert.EndsWith("</a>", result);
    }
}
