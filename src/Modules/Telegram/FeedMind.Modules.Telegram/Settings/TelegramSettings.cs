using System.ComponentModel.DataAnnotations;

namespace FeedMind.Modules.Telegram.Settings;

public sealed class TelegramSettings
{
    [Required]
    public required string ApiId { get; init; }

    [Required]
    public required string ApiHash { get; init; }

    [Required]
    public required string PhoneNumber { get; init; }

    [Required]
    public required string WTelegramSession { get; init; }

    [Required]
    public required string BotToken { get; init; }
}
