using System.Diagnostics;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Application.Posts;

public sealed class TelegramPostDispatcher
{
    private readonly ILogger<TelegramPostDispatcher> _logger;
    private readonly SubscriptionRepository _subscriptions;
    private readonly MessageRepository _messages;
    private readonly BotApiClient _botApiClient;

    public TelegramPostDispatcher(ILogger<TelegramPostDispatcher> logger, SubscriptionRepository subscriptions, MessageRepository messages, BotApiClient botApiClient)
    {
        _logger = logger;
        _subscriptions = subscriptions;
        _messages = messages;
        _botApiClient = botApiClient;
    }

    public async Task SendPostToChats(TelegramPost post, CancellationToken ct)
    {
        var successCount = 0;
        var errors = new List<string>();
        var channelId = post.ChannelId.ToString();
        var chatIds = await _subscriptions.GetActiveChatIdsByChannel(channelId, ct);

        using var dispatchActivity = Source.StartActivity("PostDispatch", ActivityKind.Producer);
        dispatchActivity?.SetTag(Tags.MessagingSystem, "telegram");
        dispatchActivity?.SetTag(Tags.MessagingOperation, "publish");
        dispatchActivity?.SetTag(Tags.MessagingDestinationName, "telegram-chats");
        dispatchActivity?.SetTag("telegram.channel_id", channelId);
        dispatchActivity?.SetTag("telegram.total_chats", chatIds.Count);

        foreach (var chatId in chatIds)
        {
            using var chatActivity = Source.StartActivity("PostDispatchToChat", ActivityKind.Producer, dispatchActivity?.Context ?? default);
            chatActivity?.SetTag(Tags.MessagingDestinationName, chatId);
            chatActivity?.SetTag(Tags.MessagingOperation, "publish");
            try
            {
                var message = await _botApiClient.SendPostToChat(chatId, post, ct);
                await _messages.Save(
                    userId: chatId,
                    botMessageId: message.Id,
                    channelId: post.ChannelId,
                    originalMessageId: post.MessageId,
                    text: post.NormalizedText,
                    cancellationToken: ct);

                successCount++;
            }
            catch (Exception exception)
            {
                chatActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                chatActivity?.AddException(exception);

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
