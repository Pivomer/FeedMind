using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using FeedMind.ChannelSyncJob.Settings;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeedMind.ChannelSyncJob;

public static class Program
{
    private const string ServiceName = "FeedMind.ChannelSyncJob";
    private const string AppHome = "APPLICATION_HOME";
    private const string ApplicationEnvironment = "APPLICATION_ENVIRONMENT";

    public static async Task<int> Main(string[] args)
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

        var builder = Host.CreateApplicationBuilder(args);

        if (!AppSettings.IsLocal(appEnvironment))
        {
            builder.Logging.ClearProviders();
        }

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();

        var credential = CreateDefaultAzureCredential();

        builder.Services.ConfigureAppSettings();
        builder.Services.AddAzureClients(credential);
        builder.Services.AddTelegramModuleCore(builder.Configuration.GetSection(TelegramModuleRegistration.SectionName), appHome);
        builder.Services.AddSingleton<Job>();

        using var host = builder.Build();

        await host.StartAsync();
        var job = host.Services.GetRequiredService<Job>();
        ExecutionResult result = await job.Run(CancellationToken.None);
        await host.StopAsync();

        return result.ExitCode;
    }

    private static DefaultAzureCredential CreateDefaultAzureCredential()
    {
        return new DefaultAzureCredential
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
            clientBuilder.AddClient<ServiceBusClient, ServiceBusClientOptions>((clientOptions, clientCredential, provider) =>
            {
                var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new ServiceBusClient(settings.ServiceBusNamespace, clientCredential, clientOptions);
            });

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
