using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Application.Handlers.Posts;

public sealed class IncomingPostHandler
{
    private readonly ILogger<IncomingPostHandler> _logger;
    private readonly UserRepository _userRepository;
    private readonly SubscriptionRepository _subscriptionRepository;

    public IncomingPostHandler(ILogger<IncomingPostHandler> logger, UserRepository userRepository, SubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleIncomingPost(IncomingPostInfo model, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing forwarded post from ChatId {ChatId} for Channel {ChannelId}", model.ChatId, model.ChannelId);

            await _userRepository.RegisterOrGetUser(model.ChatId, cancellationToken);
            await _userRepository.UpdateLastInteraction(model.ChatId, cancellationToken);
            await _subscriptionRepository.Subscribe(model.ChatId, model.ChannelId, model.ChannelName, model.Title, cancellationToken);

            _logger.LogInformation("User {ChatId} subscribed to channel {ChannelId}", model.ChatId, model.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle forwarded post from ChatId {ChatId} for Channel {ChannelId}", model.ChatId, model.ChannelId);
            throw;
        }
    }
}
