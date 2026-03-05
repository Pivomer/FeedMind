using System.ComponentModel.DataAnnotations;

namespace FeedMind.API.Settings;

public sealed class AppSettings
{
    [Required]
    public required string KeyVaultUri { get; init; }

    [Required]
    public required string AppTableServiceUri { get; init; }

    [Required]
    public required string ServiceBusNamespace { get; init; }

    private const string LocalEnvironmentName = "local";
    public const string SectionName = "AppSettings";

    public static bool IsLocal(string appEnvironment)
    {
        return LocalEnvironmentName.Equals(appEnvironment, StringComparison.OrdinalIgnoreCase);
    }
}
