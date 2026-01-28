using FeedMind.Modules.Telegram;
using FeedMind.Modules.Telegram.API.WTelegram;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class TelegramModuleRegistration
{
    public const string SectionName = "Telegram";

    public static void AddTelegramModule(this IServiceCollection services, IConfigurationSection section, string appHome)
    {
        services.AddOptions<TelegramSettings>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<SessionManager>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new SessionManager(settings, appHome);
        });
        services.AddScoped<Job>();
        services.AddSingleton<WTelegramService>();
    }
}
