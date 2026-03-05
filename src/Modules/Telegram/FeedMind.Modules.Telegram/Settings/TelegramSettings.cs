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

    [Required]
    public required string RequestsQueueName { get; init; }

    [Required]
    public required string ResultsQueueName { get; init; }

    public TimeSpan LongPollingTimeSpan = TimeSpan.FromSeconds(600);

    public const string TableServiceClientName = "TelegramTableServiceClient";
}
