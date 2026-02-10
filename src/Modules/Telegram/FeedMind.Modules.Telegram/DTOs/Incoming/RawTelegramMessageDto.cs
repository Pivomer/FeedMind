using System.Diagnostics;

namespace FeedMind.Modules.Telegram.DTOs.Incoming;

public sealed class RawTelegramMessageDto
{
    public int MessageId { get; set; }
    public string? Text { get; set; }
    public long ChannelId { get; set; }
    public ActivityContext? TraceContext { get; set; }
}
