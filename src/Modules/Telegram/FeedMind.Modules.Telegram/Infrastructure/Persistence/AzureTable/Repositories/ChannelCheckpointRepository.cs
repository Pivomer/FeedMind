using Azure;
using Azure.Data.Tables;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

public sealed class ChannelCheckpointRepository
{
    public const string TableName = "FeedMindChannelCheckpoints";

    private readonly TableClient _tableClient;
    private readonly ILogger<ChannelCheckpointRepository> _logger;

    public ChannelCheckpointRepository([FromKeyedServices(TableName)] TableClient tableClient, ILogger<ChannelCheckpointRepository> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task<int> GetCheckpoint(string channelName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<ChannelCheckpointEntity>(ChannelCheckpointEntity.FixedPartitionKey, channelName, cancellationToken: cancellationToken);
            return response.Value.LastMessageId;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return 0;
        }
    }

    public async Task SaveCheckpoint(string channelName, int lastMessageId, CancellationToken cancellationToken)
    {
        var entity = new ChannelCheckpointEntity
        {
            PartitionKey = ChannelCheckpointEntity.FixedPartitionKey,
            RowKey = channelName,
            LastMessageId = lastMessageId
        };

        try
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save checkpoint for channel {Channel} at message {MessageId}", channelName, lastMessageId);
            throw;
        }
    }
}
