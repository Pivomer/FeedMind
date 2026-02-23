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
    [InlineData("<b>bold</b>", "&lt;b&gt;bold&lt;/b&gt;")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    public void NormalizeContent_TrimsAndHandlesEmpty(string? input, string expected)
    {
        var result = PostContentUtils.NormalizeContent(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeContent_DoesNotTruncate()
    {
        var input = new string('a', 1200);
        var result = PostContentUtils.NormalizeContent(input);
        Assert.Equal(1200, result.Length);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(799, 799, false)]
    [InlineData(800, 800, false)]
    [InlineData(801, 800, true)]
    [InlineData(1200, 800, true)]
    public void BuildFormattedPostText_RespectsMaxLength(int inputLength, int expectedLength, bool shouldHaveEllipsis)
    {
        var input = new string('a', inputLength);
        var result = PostContentUtils.BuildFormattedPostText(input, -1001234567890L, 1);

        var textPart = result.Split('\n')[0];

        Assert.Equal(expectedLength + (shouldHaveEllipsis ? 1 : 0), textPart.Length);

        if (shouldHaveEllipsis)
            Assert.EndsWith("…", textPart);
        else
            Assert.DoesNotContain("…", textPart);
    }

    [Fact]
    public void BuildFormattedPostText_CutsAtLastSuitableCharacter()
    {
        var input = new string('a', 798) + " b" + new string('c', 6);
        var result = PostContentUtils.BuildFormattedPostText(input, -1001234567890L, 1);

        var textPart = result.Split('\n')[0];

        Assert.Equal(799, textPart.Length);
        Assert.EndsWith("…", textPart);
        Assert.Contains("a…", textPart);
    }

    [Theory]
    [InlineData(-1001234567890L, 42, "https://t.me/c/1234567890/42")]
    [InlineData(1234567890L, 100, "https://t.me/c/1234567890/100")]
    [InlineData(-1000000000001L, 5, "https://t.me/c/1/5")]
    [InlineData(-999999999999L, 777, "https://t.me/c/999999999999/777")]
    public void BuildFormattedPostText_GeneratesCorrectChannelUrl(long channelId, int messageId, string expectedUrlPart)
    {
        var result = PostContentUtils.BuildFormattedPostText("test content", channelId, messageId, "Open");

        Assert.StartsWith("test content", result);
        Assert.Contains($"<a href=\"{expectedUrlPart}\">Open</a>", result);
    }

    [Fact]
    public void BuildFormattedPostText_UsesDefaultLinkText()
    {
        var result = PostContentUtils.BuildFormattedPostText("hello", -1009876543210L, 123);

        Assert.StartsWith("hello", result);
        Assert.EndsWith("<a href=\"https://t.me/c/9876543210/123\">Link</a>", result);
    }

    [Theory]
    [InlineData("short text", -1001112223334L, 1)]
    [InlineData("another text", -1001112223334L, 42)]
    public void BuildFormattedPostText_ShortTextPassesThrough(string content, long channelId, int messageId)
    {
        var result = PostContentUtils.BuildFormattedPostText(content, channelId, messageId);

        Assert.StartsWith(content, result);
        Assert.Contains("<a href=\"https://t.me/c/", result);
        Assert.EndsWith("</a>", result);
    }
}
