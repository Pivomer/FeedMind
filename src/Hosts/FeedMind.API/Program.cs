using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FeedMind.API.Settings;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Azure;

namespace FeedMind.API;

public static class Program
{
    private const string ServiceName = "FeedMind.API";
    private const string AppHome = "APPLICATION_HOME";
    private const string ApplicationEnvironment = "APPLICATION_ENVIRONMENT";

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Starting: {ServiceName}");
        string? appHome = Environment.GetEnvironmentVariable(AppHome);
        string? appEnvironment = Environment.GetEnvironmentVariable(ApplicationEnvironment);

        if (appHome is null)
        {
            throw new ArgumentNullException(nameof(appHome), $"{AppHome} is required");
        }

        if (appEnvironment is null)
        {
            throw new ArgumentNullException(nameof(appEnvironment), $"{ApplicationEnvironment} is required");
        }

        var configPath = AppSettings.IsLocal(appEnvironment)
            ? Path.Combine(appHome, $"appsettings.{appEnvironment}.json")
            : Path.Combine(appHome, "appsettings.json");


        Console.WriteLine($"Starting: {AppHome}: {appHome} ConfigDirectory: {configPath} {ApplicationEnvironment}: {appEnvironment}");

        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();

        var credentials = new DefaultAzureCredential
        (
#if DEBUG
            new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = true,
                ExcludeAzureDeveloperCliCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = true
            }
#endif
        );

        builder.Services.ConfigureAppSettings();
        builder.Services.AddAzureClients(credentials);

        builder.Services.AddTelegramModule(builder.Configuration.GetSection(TelegramModuleRegistration.SectionName), appHome);

        var app = builder.Build();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");

        await app.RunAsync();
    }

    private static void ConfigureAppSettings(this IServiceCollection services)
    {
        services
            .AddOptions<AppSettings>()
            .BindConfiguration(AppSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddAzureClients(this IServiceCollection services, TokenCredential credential)
    {
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.UseCredential(credential);
            clientBuilder.AddClient<TableServiceClient, TableClientOptions>((clientOptions, clientCredential, provider) =>
            {
                var appSettings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new TableServiceClient(new Uri(appSettings.AppTableServiceUri), clientCredential, clientOptions);
            }).WithName(TelegramSettings.TableServiceClientName);

            clientBuilder.AddClient<SecretClient, SecretClientOptions>((clientOptions, clientCredential, provider) =>
            {
                var appSettings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new SecretClient(new Uri(appSettings.KeyVaultUri), clientCredential, clientOptions);
            });
        });
    }
}
