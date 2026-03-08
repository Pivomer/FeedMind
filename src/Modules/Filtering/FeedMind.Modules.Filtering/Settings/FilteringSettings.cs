using System.ComponentModel.DataAnnotations;

namespace FeedMind.Modules.Filtering.Settings;

public sealed class FilteringSettings
{
    [Required]
    public required string TelegramRequestsQueueName { get; init; }

    [Required]
    public required string TelegramResultsQueueName { get; init; }

    [Required]
    public required string OpenAiDeploymentName { get; init; }

    public TimeSpan LongPollingTimeSpan = TimeSpan.FromSeconds(600);
}
