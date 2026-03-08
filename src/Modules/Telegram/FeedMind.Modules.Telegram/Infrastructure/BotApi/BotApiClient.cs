using FeedMind.Modules.Telegram.Application.Utils;
using FeedMind.Modules.Telegram.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class BotApiClient
{
    private readonly ILogger<BotApiClient> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public BotApiClient(ILogger<BotApiClient> logger, ITelegramBotClient botClient, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _botClient = botClient;
        _scopeFactory = scopeFactory;
    }

    public async Task StartPolling(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = true,
            Limit = 100
        };
        _logger.LogInformation("Starting Bot API polling");
        await _botClient.ReceiveAsync(HandleUpdate, HandleError, receiverOptions, stoppingToken);
        _logger.LogInformation("Bot API polling completed");
    }

    private async Task HandleUpdate(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var updateHandler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();

        try
        {
            await updateHandler.HandleUpdate(client, update, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException api => $"Telegram API error [{api.ErrorCode}]: {api.Message}",
            RequestException req => $"Network/request error: {req.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Polling error: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }

    public async Task<Message> SendPostToChat(string chatId, TelegramPost postModel, bool shouldShow, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var likeString = CallbackDataParser.Feedback.BuildLike(postModel.MessageId);
            var dislikeString = CallbackDataParser.Feedback.BuildDislike(postModel.MessageId);

            var inlineKeyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("👍", likeString),
                InlineKeyboardButton.WithCallbackData("👎", dislikeString)
            );

            var formattedText = string.IsNullOrEmpty(reason)
                ? postModel.FormattedText
                : $"{postModel.FormattedText}\n\n{(shouldShow ? "🤖" : "🔴 AI: hide")} <i>{reason}</i>";

            var message = await _botClient.SendMessage(
                chatId: chatId,
                text: formattedText,
                parseMode: ParseMode.Html,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Post sent to chat {ChatId}, BotMessageId {BotMessageId}", chatId, message.Id);
            return message;
        }
        catch (ApiRequestException exception)
        {
            _logger.LogError(exception, "Failed to send post to chat {ChatId}", chatId);
            throw;
        }
    }
}
