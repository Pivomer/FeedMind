using Azure;
using Azure.Data.Tables;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;

public sealed class SubscriptionEntity : ITableEntity
{
    public required string PartitionKey { get; set; } = string.Empty;
    public required string RowKey { get; set; }

    public required string ChannelName { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset SubscribedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; } = default;
}
