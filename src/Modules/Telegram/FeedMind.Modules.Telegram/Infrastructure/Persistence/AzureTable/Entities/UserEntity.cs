using Azure;
using Azure.Data.Tables;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;

public sealed class UserEntity : ITableEntity
{
    public const string PartitionKeyValue = "User";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public required string RowKey { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastInteraction { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; } = default;
}
