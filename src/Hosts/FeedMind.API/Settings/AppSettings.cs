using System.ComponentModel.DataAnnotations;

namespace FeedMind.API.Settings;

public sealed class AppSettings
{
    private const string LocalEnvironmentName = "local";
    public const string SectionName = "AppSettings";

    [Required]
    public required string KeyVaultUri { get; init; }

    [Range(1, int.MaxValue)]
    public required int FeedParserIntervalMin { get; init; }

    public static bool IsLocal(string appEnvironment)
    {
        return LocalEnvironmentName.Equals(appEnvironment, StringComparison.OrdinalIgnoreCase);
    }
}
