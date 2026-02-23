using Azure;
using Azure.Data.Tables;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

public sealed class MessageRepository
{
    public const string TableName = "FeedMindMessages";

    private readonly TableClient _tableClient;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository([FromKeyedServices(TableName)] TableClient tableClient, ILogger<MessageRepository> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task Save(string userId, int botMessageId, long channelId, int originalMessageId, string text, CancellationToken cancellationToken = default)
    {
        var entity = new MessageEntity
        {
            PartitionKey = userId,
            RowKey = botMessageId.ToString(),
            ChannelId = channelId,
            OriginalMessageId = originalMessageId,
            Text = text,
            SentAt = DateTimeOffset.UtcNow,
            Feedback = (int)MessageFeedback.None
        };

        try
        {
            await _tableClient.AddEntityAsync(entity, cancellationToken);
            _logger.LogInformation("Saved message: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning("Message already exists: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
            throw;
        }
    }

    public async Task UpdateFeedback(string userId, int botMessageId, MessageFeedback feedback, CancellationToken cancellationToken = default)
    {
        var partitionKey = userId;
        var rowKey = botMessageId.ToString();

        try
        {
            var response = await _tableClient.GetEntityAsync<MessageEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            var existing = response.Value;

            existing.Feedback = (int)feedback;

            await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Merge, cancellationToken);
            _logger.LogInformation("Updated feedback: UserId {UserId} BotMessageId {BotMessageId} Feedback {Feedback}", userId, botMessageId, feedback);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Message not found for feedback update: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogWarning("ETag conflict while updating feedback: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update feedback: UserId {UserId} BotMessageId {BotMessageId}", userId, botMessageId);
            throw;
        }
    }
}
