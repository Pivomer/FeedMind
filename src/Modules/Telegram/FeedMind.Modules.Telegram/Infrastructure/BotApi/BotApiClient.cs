using FeedMind.Modules.Telegram.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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

    public async Task SendPostToChat(string chatId, TelegramPost postModel, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: postModel.Text,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Post sent to chat {ChatId}", chatId);
        }
        catch (ApiRequestException exception)
        {
            _logger.LogError(exception, "Failed to send post to chat {ChatId}", chatId);
            throw;
        }
    }
}
