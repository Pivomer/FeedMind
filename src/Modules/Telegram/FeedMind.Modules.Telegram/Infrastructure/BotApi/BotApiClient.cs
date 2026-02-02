using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class BotApiClient
{
    private readonly ILogger<BotApiClient> _logger;
    private readonly TelegramSettings _settings;
    private readonly ITelegramBotClient _botClient;
    private readonly BotUpdateHandler _updateHandler;

    public BotApiClient(ILogger<BotApiClient> logger, IOptions<TelegramSettings> options, ITelegramBotClient botClient, BotUpdateHandler botUpdateHandler)
    {
        _logger = logger;
        _settings = options.Value;
        _botClient = botClient;
        _updateHandler = botUpdateHandler;
    }

    public async Task StartPolling(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            DropPendingUpdates = true,
            Limit = 100
        };
        _logger.LogInformation("Starting Bot API polling");
        await _botClient.ReceiveAsync(HandleUpdate, HandleError, receiverOptions, stoppingToken);
        _logger.LogInformation("Bot API polling completed");
    }

    private async Task HandleUpdate(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        try
        {
            await _updateHandler.HandleUpdate(client, update, cancellationToken);
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

    public async Task SendPostToChats(TelegramPost postModel, CancellationToken cancellationToken)
    {
        var successCount = 0;
        var failureCount = 0;
        var errors = new List<string>();

        foreach (var chatId in _settings.ChatIds)
        {
            try
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: postModel.Text,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                successCount++;
                _logger.LogInformation("Post sent to chat {ChatId}", chatId);
            }
            catch (ApiRequestException apiEx)
            {
                failureCount++;
                var error = $"Chat {chatId}: API error [{apiEx.ErrorCode}] - {apiEx.Message}";
                errors.Add(error);
                _logger.LogError(apiEx, "Failed to send post to chat {ChatId}", chatId);
            }
            catch (Exception exception)
            {
                failureCount++;
                var error = $"Chat {chatId}: {exception.Message}";
                errors.Add(error);
                _logger.LogError(exception, "Failed to send post to chat {ChatId}", chatId);
            }
        }
        if (successCount == 0 && failureCount > 0)
        {
            var errorSummary = string.Join("; ", errors);
            throw new InvalidOperationException($"Failed to send post to all {failureCount} configured chat(s). Errors: {errorSummary}");
        }
        if (failureCount > 0)
        {
            _logger.LogWarning("Post sent partially: {SuccessCount}/{TotalCount} chats succeeded, {FailureCount} failed", successCount, _settings.ChatIds.Count, failureCount);
        }
    }
}
