using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Application.Handlers.Commands;

public sealed class UnsubscribeHandler
{
    private readonly ILogger<UnsubscribeHandler> _logger;
    private readonly SubscriptionRepository _subscriptionRepository;

    public UnsubscribeHandler(ILogger<UnsubscribeHandler> logger, SubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleUnsubscribe(long chatId, string channelId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Unsubscribing ChatId {ChatId} from Channel {ChannelId}", chatId, channelId);
            await _subscriptionRepository.Unsubscribe(chatId.ToString(), channelId, cancellationToken);
            _logger.LogInformation("ChatId {ChatId} successfully unsubscribed from Channel {ChannelId}", chatId, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe ChatId {ChatId} from Channel {ChannelId}", chatId, channelId);
            throw;
        }
    }

    public async Task<int> HandleUnsubscribeAll(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Unsubscribing ChatId {ChatId} from all channels", chatId);
            var count = await _subscriptionRepository.UnsubscribeAll(chatId.ToString(), cancellationToken);
            _logger.LogInformation("ChatId {ChatId} unsubscribed from {Count} channels", chatId, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe ChatId {ChatId} from all channels", chatId);
            throw;
        }
    }
}
