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

    public async Task Save(string chatId, int botMessageId, long channelId, int originalMessageId, string text, CancellationToken cancellationToken = default)
    {
        var entity = new MessageEntity
        {
            PartitionKey = chatId,
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
            _logger.LogInformation("Saved message: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            _logger.LogWarning("Message already exists: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save message: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
            throw;
        }
    }

    public async Task UpdateFeedback(long chatId, int botMessageId, MessageFeedback feedback, CancellationToken cancellationToken = default)
    {
        var partitionKey = chatId.ToString();
        var rowKey = botMessageId.ToString();

        try
        {
            var response = await _tableClient.GetEntityAsync<MessageEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            var existing = response.Value;

            existing.Feedback = (int)feedback;

            await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Merge, cancellationToken);
            _logger.LogInformation("Updated feedback: ChatId {ChatId} BotMessageId {BotMessageId} Feedback {Feedback}", chatId, botMessageId, feedback);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            _logger.LogWarning("Message not found for feedback update: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            _logger.LogWarning("ETag conflict while updating feedback: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update feedback: ChatId {ChatId} BotMessageId {BotMessageId}", chatId, botMessageId);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetByFeedback(string chatId, long channelId, int feedback, int take, CancellationToken cancellationToken)
    {
        try
        {
            return await _tableClient.QueryAsync<MessageEntity>(x =>
                    x.PartitionKey == chatId
                    && x.ChannelId == channelId
                    && x.Feedback == feedback, cancellationToken: cancellationToken)
                .OrderByDescending(x => x.SentAt)
                .Take(take)
                .Select(x => x.Text)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get feedback messages: ChatId {ChatId} ChannelId {ChannelId}", chatId, channelId);
            throw;
        }
    }
}
