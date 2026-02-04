namespace FeedMind.Modules.Telegram.Application.Commands;

public record Command(CommandName Name, string? ChannelId);
