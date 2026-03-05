using System.Diagnostics;
using FeedMind.Modules.Filtering.Contracts;
using FeedMind.Modules.Telegram.Application.Filtering;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using FeedMind.Modules.Telegram.Infrastructure.ServiceBus;
using Microsoft.Extensions.Logging;
using static FeedMind.Modules.Telegram.Telemetry;

namespace FeedMind.Modules.Telegram.Application.Posts;

public sealed class TelegramPostDispatcher
{
    private readonly ILogger<TelegramPostDispatcher> _logger;
    private readonly SubscriptionRepository _subscriptions;
    private readonly FilterRequestBuilder _filterRequestBuilder;
    private readonly ServiceBusPublisher _serviceBusPublisher;
    private readonly MessageRepository _messages;
    private readonly BotApiClient _botApiClient;

    public TelegramPostDispatcher(
        ILogger<TelegramPostDispatcher> logger,
        SubscriptionRepository subscriptions,
        FilterRequestBuilder filterRequestBuilder,
        ServiceBusPublisher serviceBusPublisher,
        MessageRepository messages,
        BotApiClient botApiClient)
    {
        _logger = logger;
        _subscriptions = subscriptions;
        _filterRequestBuilder = filterRequestBuilder;
        _serviceBusPublisher = serviceBusPublisher;
        _messages = messages;
        _botApiClient = botApiClient;
    }

    public async Task Dispatch(TelegramPost postModel, CancellationToken stoppingToken)
    {
        var channelId = postModel.ChannelId;
        var chatIds = await _subscriptions.GetActiveChatIdsByChannel(channelId, stoppingToken);

        foreach (var chatId in chatIds)
        {
            var filterRequest = await _filterRequestBuilder.Build(
                chatId: chatId,
                channelId: channelId,
                messageId: postModel.MessageId,
                text: postModel.NormalizedText,
                ct: stoppingToken);

            await _serviceBusPublisher.Publish(filterRequest, stoppingToken);
        }
    }

    public async Task Deliver(TelegramFilterResult result, CancellationToken cancellationToken)
    {
        if (!result.ShouldShow)
        {
            _logger.LogInformation("Post {MessageId} hidden for ChatId {ChatId}. Reason: {Reason}", result.MessageId, result.ChatId, result.Reason);
            return;
        }

        var post = TelegramPost.FromFiltered(
            channelId: result.ChannelId,
            messageId: result.MessageId,
            normalizedText: result.Text);

        using var activity = Source.StartActivity(Operations.PostDeliver, ActivityKind.Consumer);
        activity?.SetTag(Tags.MessagingSystem, "telegram");
        activity?.SetTag(Tags.MessagingOperation, "publish");
        activity?.SetTag(Tags.TelegramChannelId, result.ChannelId);
        activity?.SetTag(Tags.TelegramChatId, result.ChatId);

        try
        {
            var chatId = result.ChatId;
            var message = await _botApiClient.SendPostToChat(chatId, post, cancellationToken);
            await _messages.Save(
                chatId: chatId,
                botMessageId: message.Id,
                channelId: post.ChannelId,
                originalMessageId: post.MessageId,
                text: post.NormalizedText,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            _logger.LogError(exception, "Failed to deliver post {MessageId} to chat {ChatId}", result.MessageId, result.ChatId);
            throw;
        }
    }
}
