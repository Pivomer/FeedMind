using FeedMind.Modules.Telegram.Application.Utils;
using FeedMind.Modules.Telegram.Domain.Models;
using Xunit;

namespace UnitTests.TelegramModule.Application.Utils;

public sealed class CallbackDataParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1")]
    [InlineData(":like:123")]
    [InlineData("99:like:123")]
    [InlineData("1:like:abc")]
    [InlineData("1:unknown:123")]
    [InlineData("1:like:")]
    [InlineData("1::123")]
    [InlineData("0:like:123")]
    public void Parse_ReturnsNull_ForInvalidInputs(string? input)
    {
        var result = CallbackDataParser.Parse(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1:like:123", 123, MessageFeedback.Like)]
    [InlineData("1:dislike:123", 123, MessageFeedback.Dislike)]
    [InlineData("1:like:1", 1, MessageFeedback.Like)]
    [InlineData("1:dislike:999999", 999999, MessageFeedback.Dislike)]
    public void Parse_ReturnsFeedback_ForValidInput(string input, int expectedMessageId, MessageFeedback expectedFeedback)
    {
        var result = CallbackDataParser.Parse(input);

        var feedback = Assert.IsType<CallbackData.Feedback>(result);
        Assert.Equal(expectedMessageId, feedback.MessageId);
        Assert.Equal(expectedFeedback, feedback.Feed);
    }

    [Theory]
    [InlineData(123, MessageFeedback.Like)]
    [InlineData(456, MessageFeedback.Dislike)]
    public void BuildAndParse_RoundTrip(int messageId, MessageFeedback feedbackType)
    {
        var raw = feedbackType == MessageFeedback.Like
            ? CallbackDataParser.Feedback.BuildLike(messageId)
            : CallbackDataParser.Feedback.BuildDislike(messageId);

        var result = CallbackDataParser.Parse(raw);

        var feedback = Assert.IsType<CallbackData.Feedback>(result);
        Assert.Equal(messageId, feedback.MessageId);
        Assert.Equal(feedbackType, feedback.Feed);
    }

    [Theory]
    [InlineData(123)]
    [InlineData(999999)]
    public void BuildLike_ProducesCorrectFormat(int messageId)
    {
        var result = CallbackDataParser.Feedback.BuildLike(messageId);
        Assert.StartsWith("1:like:", result);
        Assert.EndsWith(messageId.ToString(), result);
    }

    [Theory]
    [InlineData(123)]
    [InlineData(999999)]
    public void BuildDislike_ProducesCorrectFormat(int messageId)
    {
        var result = CallbackDataParser.Feedback.BuildDislike(messageId);
        Assert.StartsWith("1:dislike:", result);
        Assert.EndsWith(messageId.ToString(), result);
    }
}
