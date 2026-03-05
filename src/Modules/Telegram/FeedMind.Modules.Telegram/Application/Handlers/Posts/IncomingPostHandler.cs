using FeedMind.Modules.Telegram.Application.Channels;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Application.Handlers.Posts;

public sealed class IncomingPostHandler
{
    private readonly ILogger<IncomingPostHandler> _logger;
    private readonly UserRepository _userRepository;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly ChannelSubscriptionManager _subscriptionManager;

    public IncomingPostHandler(ILogger<IncomingPostHandler> logger, UserRepository userRepository, SubscriptionRepository subscriptionRepository, ChannelSubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _subscriptionManager = subscriptionManager;
    }

    public async Task HandleIncomingPost(IncomingPostInfo model, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing forwarded post from ChatId {ChatId} for Channel {ChannelId}", model.ChatId, model.ChannelId);

            await _userRepository.RegisterOrGetUser(model.ChatId, cancellationToken);
            await _userRepository.UpdateLastInteraction(model.ChatId, cancellationToken);
            await _subscriptionRepository.Subscribe(model.ChatId, model.ChannelId, model.ChannelName, model.Title, cancellationToken);
            await _subscriptionManager.Sync(cancellationToken);

            _logger.LogInformation("ChatId {ChatId} subscribed to channel {ChannelId}", model.ChatId, model.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle forwarded post from ChatId {ChatId} for Channel {ChannelId}", model.ChatId, model.ChannelId);
            throw;
        }
    }
}
