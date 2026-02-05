namespace FeedMind.Modules.Telegram.Domain.Models;

public sealed record IncomingPostInfo(string ChatId,  string ChannelId, string ChannelName, string? Title);
