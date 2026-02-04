using FeedMind.Modules.Telegram.Application.Commands;
using FeedMind.Modules.Telegram.Domain.Models;
using Telegram.Bot.Types;

namespace FeedMind.Modules.Telegram.Application.Utils;

public static class CommandParser
{
    private static readonly Dictionary<string, CommandName> _commandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["start"] = CommandName.Start,
        ["unsubscribe"] = CommandName.Unsubscribe,
        ["help"] = CommandName.Help
    };

    public static Command? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var span = text.AsSpan().TrimStart();
        if (span.IsEmpty || span[0] != '/')
        {
            return null;
        }

        span = span[1..];
        if (span.IsEmpty)
        {
            return null;
        }

        var commandEnd = 0;
        while (commandEnd < span.Length && !char.IsWhiteSpace(span[commandEnd]))
        {
            var c = span[commandEnd];
            if (c == '@' || !IsValidCommandChar(c))
            {
                return null;
            }

            commandEnd++;
        }

        if (commandEnd == 0)
        {
            return null;
        }

        var commandSpan = span[..commandEnd];
        if (!_commandMap.TryGetValue(commandSpan.ToString(), out var commandName))
        {
            return null;
        }

        var argsSpan = span[commandEnd..].TrimStart();
        if (argsSpan.IsEmpty)
        {
            return new Command(commandName, null);
        }

        var argEnd = 0;
        while (argEnd < argsSpan.Length && !char.IsWhiteSpace(argsSpan[argEnd]))
        {
            argEnd++;
        }

        var argSpan = argsSpan[..argEnd];
        if (argsSpan[argEnd..].TrimStart().Length > 0)
        {
            return null;
        }

        if (!IsValidArgument(argSpan))
        {
            return null;
        }

        if (commandName is CommandName.Start or CommandName.Help)
        {
            return null;
        }

        return new Command(commandName, argSpan.ToString());
    }

    private static bool IsValidCommandChar(char argument)
    {
        return (argument >= 'a' && argument <= 'z') || (argument >= 'A' && argument <= 'Z') || (argument >= '0' && argument <= '9') || argument == '_';
    }

    private static bool IsValidArgument(ReadOnlySpan<char> argument)
    {
        if (argument.IsEmpty)
        {
            return false;
        }

        if (argument[0] == '@')
        {
            if (argument.Length == 1)
            {
                return false;
            }

            for (var i = 1; i < argument.Length; i++)
            {
                if (!IsValidCommandChar(argument[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (argument.StartsWith("-100") && argument.Length > 4)
        {
            for (var i = 4; i < argument.Length; i++)
            {
                if (!char.IsDigit(argument[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    public static IncomingPostInfo? GetIncomingPostInfo(Message message)
    {
        var channel = message.ForwardFromChat;
        if (channel?.Username is null)
        {
            return null;
        }

        return new IncomingPostInfo(
            message.Chat.Id.ToString(),
            channel.Id.ToString(),
            channel.Username,
            channel.Title
        );
    }
}
