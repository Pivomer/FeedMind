using Azure;
using Azure.Data.Tables;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;

public sealed class ChannelCheckpointEntity : ITableEntity
{
    public const string FixedPartitionKey = "channel-checkpoint";

    public required string PartitionKey { get; set; } = FixedPartitionKey;

    public required string RowKey { get; set; } //ChannelName

    public int LastMessageId { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; } = default;
}
