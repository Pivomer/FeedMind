using FeedMind.Modules.Filtering.Application;
using FeedMind.Modules.Filtering.Application.Telegram;
using FeedMind.Modules.Filtering.Infrastructure.ServiceBus;
using FeedMind.Modules.Filtering.Services.Background;
using FeedMind.Modules.Filtering.Services.Health;
using FeedMind.Modules.Filtering.Settings;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class FilteringModuleRegistration
{
    public const string SectionName = "Filtering";

    extension(IServiceCollection services)
    {
        public void AddFilteringModule(IConfigurationSection section)
        {
            services.AddOptions<FilteringSettings>()
                .Bind(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<WorkerStates>();
            services.AddSingleton<TelegramResultPublisher>();
            services.AddSingleton<OpenAiFilterClient>();
            services.AddSingleton<TelegramFilteringHandler>();

            services.AddHostedService<TelegramRequestConsumerService>();
            services.AddHealthChecks().AddCheck<WorkersHealthCheck>("filtering-workers");
        }
    }
}
