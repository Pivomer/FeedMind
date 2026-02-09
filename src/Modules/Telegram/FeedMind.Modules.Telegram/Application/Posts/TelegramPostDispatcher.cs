using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Application.Posts;

public sealed class TelegramPostDispatcher
{
    private readonly ILogger<TelegramPostDispatcher> _logger;
    private readonly SubscriptionRepository _subscriptions;
    private readonly BotApiClient _botApiClient;

    public TelegramPostDispatcher(ILogger<TelegramPostDispatcher> logger, SubscriptionRepository subscriptions, BotApiClient botApiClient)
    {
        _logger = logger;
        _subscriptions = subscriptions;
        _botApiClient = botApiClient;
    }

    public async Task SendPostToChats(TelegramPost post, CancellationToken ct)
    {
        var successCount = 0;
        var errors = new List<string>();
        var chanelId = post.ChannelId.ToString();
        var chatIds = await _subscriptions.GetActiveChatIdsByChannel(chanelId, ct);

        foreach (var chatId in chatIds)
        {
            try
            {
                await _botApiClient.SendPostToChat(chatId, post, ct);
                successCount++;
            }
            catch (Exception exception)
            {
                var error = $"Chat {chatId}: {exception.Message}";
                errors.Add(error);
                _logger.LogError(exception, "Failed to send post to chat {ChatId}", chatId);
            }
        }

        var failureCount = chatIds.Count - successCount;
        if (successCount == 0 && failureCount != 0)
        {
            var errorSummary = string.Join("; ", errors);
            throw new InvalidOperationException($"Failed to send post to all {failureCount} configured chat(s). Errors: {errorSummary}");
        }

        if (failureCount > 0)
        {
            _logger.LogWarning("Post sent partially: {SuccessCount}/{TotalCount} chats succeeded, {FailureCount} failed", successCount, chatIds.Count, failureCount);
        }
    }
}
