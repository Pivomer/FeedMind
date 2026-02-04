using FeedMind.Modules.Telegram.Application.Commands;
using FeedMind.Modules.Telegram.Application.Utils;
using Xunit;

namespace UnitTests.TelegramModule.Application.Utils;

public sealed class CommandParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("start")]
    [InlineData("/")]
    [InlineData("/ ")]
    [InlineData("/@bot")]
    [InlineData("/unknown")]
    [InlineData("/start!")]
    [InlineData("/start-")]
    [InlineData("/unsubscribe@bot")]
    [InlineData("/unsubscribe@bot @telegram")]
    [InlineData("/start@mybot")]
    [InlineData("/help@bot")]
    [InlineData("/unsubscribe @")]
    [InlineData("/unsubscribe abc")]
    [InlineData("/unsubscribe 123abc")]
    [InlineData("/unsubscribe -")]
    [InlineData("/unsubscribe -123abc")]
    [InlineData("/unsubscribe -100")]
    [InlineData("/unsubscribe @telegram extra")]
    [InlineData("/unsubscribe -1001234567890 extra")]
    [InlineData("/start arg")]
    [InlineData("/help arg")]
    [InlineData("/unsubscribe @user!name")]
    [InlineData("/unsubscribe @user-name")]
    [InlineData("/unsubscribe @user.name")]
    [InlineData("/unsubscribe 123")]
    [InlineData("/unsubscribe -123")]
    public void Parse_ReturnsNull_ForInvalidInputs(string input)
    {
        var result = CommandParser.Parse(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("/start", CommandName.Start, null)]
    [InlineData("/start ", CommandName.Start, null)]
    [InlineData("/START", CommandName.Start, null)]
    [InlineData("/Start", CommandName.Start, null)]
    [InlineData("/help", CommandName.Help, null)]
    [InlineData("/HELP ", CommandName.Help, null)]
    [InlineData("/Help", CommandName.Help, null)]
    [InlineData("/unsubscribe", CommandName.Unsubscribe, null)]
    [InlineData("/unsubscribe ", CommandName.Unsubscribe, null)]
    [InlineData("/UNSUBSCRIBE", CommandName.Unsubscribe, null)]
    [InlineData("/Unsubscribe", CommandName.Unsubscribe, null)]
    public void Parse_RecognizesCommandsWithoutArguments(string input, CommandName expectedName, string? expected)
    {
        var result = CommandParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expected, result.ChannelId);
    }

    [Theory]
    [InlineData("/unsubscribe @telegram", CommandName.Unsubscribe, "@telegram")]
    [InlineData("/unsubscribe @durov", CommandName.Unsubscribe, "@durov")]
    [InlineData("/unsubscribe @durov_news", CommandName.Unsubscribe, "@durov_news")]
    [InlineData("/unsubscribe @Telegram", CommandName.Unsubscribe, "@Telegram")]
    [InlineData("/unsubscribe @TEST123", CommandName.Unsubscribe, "@TEST123")]
    [InlineData("/unsubscribe @a", CommandName.Unsubscribe, "@a")]
    [InlineData("/UNSUBSCRIBE @telegram", CommandName.Unsubscribe, "@telegram")]
    [InlineData("/Unsubscribe @telegram", CommandName.Unsubscribe, "@telegram")]
    [InlineData("/unsubscribe -1001234567890", CommandName.Unsubscribe, "-1001234567890")]
    [InlineData("/unsubscribe -1009876543210", CommandName.Unsubscribe, "-1009876543210")]
    [InlineData("/unsubscribe -100123456789012345", CommandName.Unsubscribe, "-100123456789012345")]
    public void Parse_RecognizesCommandsWithValidArguments(string input, CommandName expectedName, string? expected)
    {
        var result = CommandParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expected, result.ChannelId);
    }

    [Fact]
    public void Parse_HandlesMultipleSpaces()
    {
        var result = CommandParser.Parse("/unsubscribe   @telegram   ");

        Assert.NotNull(result);
        Assert.Equal(CommandName.Unsubscribe, result.Name);
        Assert.Equal("@telegram", result.ChannelId);
    }

    [Fact]
    public void Parse_IgnoresLeadingTrailingSpaces()
    {
        var result = CommandParser.Parse("   /start   ");

        Assert.NotNull(result);
        Assert.Equal(CommandName.Start, result.Name);
        Assert.Null(result.ChannelId);
    }

    [Fact]
    public void Parse_IgnoresLeadingTrailingSpacesWithArgument()
    {
        var result = CommandParser.Parse("   /unsubscribe   @telegram   ");

        Assert.NotNull(result);
        Assert.Equal(CommandName.Unsubscribe, result.Name);
        Assert.Equal("@telegram", result.ChannelId);
    }
}
