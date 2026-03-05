using Azure;
using Azure.Data.Tables;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;

public sealed class MessageEntity : ITableEntity
{
    public required string PartitionKey { get; set; } = string.Empty; // ChatId
    public required string RowKey { get; set; } // BotMessageId

    public required long ChannelId { get; set; }
    public required int OriginalMessageId { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public int Feedback { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; } = default;
}
