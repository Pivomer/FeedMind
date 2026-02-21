using FeedMind.Modules.Telegram.Application.Commands;
using FeedMind.Modules.Telegram.Application.Handlers.Commands;
using FeedMind.Modules.Telegram.Application.Handlers.Posts;
using FeedMind.Modules.Telegram.Application.Utils;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly IncomingPostHandler _incomingPostHandler;
    private readonly UnsubscribeHandler _unsubscribeHandler;

    public MessageHandler(ILogger<MessageHandler> logger, IncomingPostHandler incomingPostHandler, UnsubscribeHandler unsubscribeHandler)
    {
        _logger = logger;
        _incomingPostHandler = incomingPostHandler;
        _unsubscribeHandler = unsubscribeHandler;
    }

    public async Task HandleMessage(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.ForwardFromChat != null)
            {
                _logger.LogInformation("Received forwarded message from ChatId {ChatId}", message.Chat.Id);
                await HandleForwardedMessage(bot, message, cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Text))
            {
                return;
            }

            var parsedCommand = CommandParser.Parse(message.Text);
            if (parsedCommand is null)
            {
                _logger.LogWarning("Failed to parse command from ChatId {ChatId}: {Text}", message.Chat.Id, message.Text);
                return;
            }

            using var activity = Source.StartActivity(Operations.MessageHandle);
            activity?.SetTag(Tags.TelegramChatId, message.Chat.Id.ToString());
            activity?.SetTag(Tags.TelegramCommandName, parsedCommand.Name.ToString());

            _logger.LogInformation("Processing command {Command} from ChatId {ChatId}", parsedCommand.Name, message.Chat.Id);
            switch (parsedCommand.Name)
            {
                case CommandName.Start:
                    await bot.SendMessage(message.Chat.Id, "Welcome to FeedMind Bot!", cancellationToken: cancellationToken);
                    break;

                case CommandName.Unsubscribe:
                    await HandleUnsubscribeCommand(bot, parsedCommand, message.Chat.Id, cancellationToken);
                    break;

                case CommandName.Help:
                    await bot.SendMessage(
                        message.Chat.Id,
                        "Available commands:\n" +
                        "/start — start bot\n" +
                        "/unsubscribe — unsubscribe all\n" +
                        "/help — show this message\n" +
                        "/status — bot status (coming soon)",
                        cancellationToken: cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unknown command {Command} from ChatId {ChatId}", parsedCommand.Name, message.Chat.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle message from ChatId {ChatId}", message.Chat.Id);
            throw;
        }
    }

    private async Task HandleUnsubscribeCommand(ITelegramBotClient bot, Command command, long chatId, CancellationToken cancellationToken)
    {
        if (command.ChannelId is not null)
        {
            var channelId = command.ChannelId;
            await _unsubscribeHandler.HandleUnsubscribe(chatId, channelId, cancellationToken);
            await bot.SendMessage(chatId, $"You have unsubscribed from {channelId}", cancellationToken: cancellationToken);
            return;
        }

        var count = await _unsubscribeHandler.HandleUnsubscribeAll(chatId, cancellationToken);
        await bot.SendMessage(chatId, $"You have unsubscribed from {count} channels.", cancellationToken: cancellationToken);
    }

    private async Task HandleForwardedMessage(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        var model = CommandParser.GetIncomingPostInfo(message);
        if (model == null)
        {
            return;
        }

        using var activity = Source.StartActivity(Operations.MessageHandle);
        activity?.SetTag(Tags.TelegramChatId, message.Chat.Id.ToString());
        activity?.SetTag(Tags.TelegramMessageType, "forwarded");

        await _incomingPostHandler.HandleIncomingPost(model, cancellationToken);
        await bot.SendMessage(message.Chat.Id, $"You have subscribed to channel @{model.ChannelName}", cancellationToken: cancellationToken);
    }
}
