using Azure;
using Azure.Data.Tables;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

public sealed class SubscriptionRepository
{
    public const string TableName = "FeedMindTelegramSubscriptions";

    private readonly TableClient _tableClient;
    private readonly ILogger<SubscriptionRepository> _logger;

    public SubscriptionRepository([FromKeyedServices(TableName)] TableClient tableClient, ILogger<SubscriptionRepository> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task Subscribe(string chatId, string channelId, string channelName, string? title, CancellationToken cancellationToken = default)
    {
        var partitionKey = chatId;
        var rowKey = channelId;
        try
        {
            var response = await _tableClient.GetEntityAsync<SubscriptionEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            var existing = response.Value;

            if (existing.IsActive)
            {
                return;
            }

            existing.IsActive = true;
            existing.ChannelName = channelName;
            existing.Title = title;

            await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Merge, cancellationToken);
            _logger.LogInformation("Re-activated subscription: ChatId {ChatId} Channel {ChannelId}", chatId, channelId);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            var entity = new SubscriptionEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                ChannelName = channelName,
                Title = title,
                SubscribedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };

            try
            {
                await _tableClient.AddEntityAsync(entity, cancellationToken);
                _logger.LogInformation("Created new subscription: ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
            }
            catch (RequestFailedException failedException) when (failedException.Status == 409)
            {
                _logger.LogWarning("Race condition: subscription already exists ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to subscribe ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
            throw;
        }
    }

    public async Task Unsubscribe(string chatId, string channelId, CancellationToken cancellationToken = default)
    {
        var partitionKey = chatId;
        var rowKey = channelId;
        try
        {
            var response = await _tableClient.GetEntityAsync<SubscriptionEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            var existing = response.Value;

            if (!existing.IsActive)
            {
                return;
            }

            existing.IsActive = false;

            await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Merge, cancellationToken);
            _logger.LogInformation("Unsubscribed ChatId {ChatId} from Channel {ChannelId}", chatId, channelId);
        }
        catch (RequestFailedException failedException) when (failedException.Status == 404)
        {
            _logger.LogWarning("Unsubscribe failed: subscription not found ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
        }
        catch (RequestFailedException failedException) when (failedException.Status == 412)
        {
            _logger.LogWarning("ETag conflict while unsubscribing ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to unsubscribe ChatId {ChatId} - Channel {ChannelId}", chatId, channelId);
            throw;
        }
    }

    public async Task<int> UnsubscribeAll(string chatId, CancellationToken cancellationToken)
    {
        var count = 0;

        await foreach (var entity in _tableClient.QueryAsync<SubscriptionEntity>(x => x.PartitionKey == chatId && x.IsActive, cancellationToken: cancellationToken))
        {
            entity.IsActive = false;

            try
            {
                await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
                count++;
            }
            catch (RequestFailedException failedException) when (failedException.Status == 412)
            {
                _logger.LogWarning("ETag conflict while unsubscribing all: ChatId {ChatId} - Channel {ChannelId}", chatId, entity.RowKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to unsubscribe ChatId {ChatId} - Channel {ChannelId}", chatId, entity.RowKey);
            }
        }

        _logger.LogInformation("Unsubscribed ChatId {ChatId} from {Count} channels", chatId, count);

        return count;
    }

    public async Task<HashSet<string>> GetDistinctActiveChannelNames(CancellationToken cancellationToken)
    {
        var hashSet = new HashSet<string>();
        await foreach (var entity in _tableClient.QueryAsync<SubscriptionEntity>(x => x.IsActive, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(entity.ChannelName))
            {
                hashSet.Add(entity.ChannelName);
            }
        }

        return hashSet;
    }

    public async Task RemoveChannel(string channelName, CancellationToken cancellationToken)
    {
        var count = 0;

        await foreach (var entity in _tableClient.QueryAsync<SubscriptionEntity>(x => x.ChannelName == channelName, cancellationToken: cancellationToken))
        {
            try
            {
                await _tableClient.DeleteEntityAsync(entity, cancellationToken: cancellationToken);
                count++;
            }
            catch (RequestFailedException failedException) when (failedException.Status == 412)
            {
                _logger.LogWarning("ETag conflict while removing channel {ChannelName} for ChatId {ChatId}", channelName, entity.PartitionKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to remove channel {ChannelName} for ChatId {ChatId}", channelName, entity.PartitionKey);
            }
        }

        _logger.LogInformation("Channel {ChannelName} hard-removed from {Count} subscriptions", channelName, count);
    }

    public async Task<HashSet<string>> GetActiveChatIdsByChannel(string channelId, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>();

        await foreach (var entity in _tableClient.QueryAsync<SubscriptionEntity>(x => x.IsActive && x.RowKey == channelId, select: ["PartitionKey"], cancellationToken: cancellationToken))
        {
            result.Add(entity.PartitionKey);
        }

        return result;
    }
}
