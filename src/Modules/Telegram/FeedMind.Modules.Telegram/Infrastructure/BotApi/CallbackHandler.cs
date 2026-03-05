using FeedMind.Modules.Telegram.Application.Utils;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class CallbackHandler
{
    private readonly ILogger<CallbackHandler> _logger;
    private readonly MessageRepository _messages;

    public CallbackHandler(ILogger<CallbackHandler> logger, MessageRepository messages)
    {
        _logger = logger;
        _messages = messages;
    }

    public async Task Handle(ITelegramBotClient bot, CallbackQuery callback, CancellationToken cancellationToken)
    {
        var data = callback.Data;

        if (callback.Message is not { } message)
        {
            var chatId = callback.From.Id.ToString();
            _logger.LogWarning("Received callback with missing message. ChatId {ChatId}", chatId);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        switch (CallbackDataParser.Parse(data))
        {
            case CallbackData.Feedback feedback:
                await HandleFeedback(bot, message, feedback, cancellationToken);
                break;
            default:
                _logger.LogWarning("Unknown callback data: {Data}", data);
                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleFeedback(ITelegramBotClient bot, Message botMessage, CallbackData.Feedback feedback, CancellationToken cancellationToken)
    {
        var chatId = botMessage.Chat.Id;
        await _messages.UpdateFeedback(chatId, botMessage.Id, feedback.Feed, cancellationToken);

        var likeString = CallbackDataParser.Feedback.BuildLike(feedback.MessageId);
        var dislikeString = CallbackDataParser.Feedback.BuildDislike(feedback.MessageId);

        var updatedKeyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(feedback.Feed == MessageFeedback.Like ? "👍 ✓" : "👍", likeString),
            InlineKeyboardButton.WithCallbackData(feedback.Feed == MessageFeedback.Dislike ? "👎 ✓" : "👎", dislikeString)
        );

        await bot.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: botMessage.Id,
            replyMarkup: updatedKeyboard,
            cancellationToken: cancellationToken);
        _logger.LogInformation("Feedback {Feedback} saved. ChatId {ChatId} BotMessageId {BotMessageId}", feedback, chatId, feedback.MessageId);
    }
}
