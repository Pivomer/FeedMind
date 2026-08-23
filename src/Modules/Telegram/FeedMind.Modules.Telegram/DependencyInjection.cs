using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using FeedMind.Modules.Telegram.Application.Channels;
using FeedMind.Modules.Telegram.Application.Filtering;
using FeedMind.Modules.Telegram.Application.Handlers.Commands;
using FeedMind.Modules.Telegram.Application.Handlers.Posts;
using FeedMind.Modules.Telegram.Application.Posts;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using FeedMind.Modules.Telegram.Infrastructure.ServiceBus;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using FeedMind.Modules.Telegram.Infrastructure.Wclient.Handlers;
using FeedMind.Modules.Telegram.Services.Background;
using FeedMind.Modules.Telegram.Services.Health;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramMessageMapper = FeedMind.Modules.Telegram.Mappings.TelegramMessageMapper;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class TelegramModuleRegistration
{
    public const string SectionName = "Telegram";

    extension(IServiceCollection services)
    {
        public void AddTelegramModuleCore(IConfigurationSection section, string appHome)
        {
            services.AddOptions<TelegramSettings>()
                .Bind(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<JoinChannelErrorHandler>();
            services.AddSingleton<TelegramMessageMapper>();
            services.AddSingleton<SessionManager>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
                return new SessionManager(settings, appHome);
            });
            services.AddSingleton<ITelegramBotClient>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
                var secretClient = provider.GetRequiredService<SecretClient>();
                var token = secretClient.GetSecret(settings.BotToken).Value.Value;
                return new TelegramBotClient(token);
            });
            services.AddSingleton<BotApiClient>();

            services.AddSingleton<WTelegramClient>();
            services.AddScoped<BotUpdateHandler>();
            services.AddScoped<MessageHandler>();
            services.AddScoped<CallbackHandler>();

            services.AddTelegramHandlers();
            services.AddRepository<UserRepository>(UserRepository.TableName);
            services.AddRepository<SubscriptionRepository>(SubscriptionRepository.TableName);
            services.AddRepository<MessageRepository>(MessageRepository.TableName);
            services.AddRepository<ChannelCheckpointRepository>(ChannelCheckpointRepository.TableName);

            services.AddSingleton<ChannelSubscriptionManager>();
            services.AddSingleton<TelegramPostDispatcher>();
            services.AddSingleton<FilterRequestBuilder>();
            services.AddSingleton<ServiceBusPublisher>();

            services.AddSingleton<WorkerStates>();
        }

        public void AddTelegramModule(IConfigurationSection section, string appHome)
        {
            services.AddTelegramModuleCore(section, appHome);

            services.AddHostedService<BotPollingService>();
            services.AddHostedService<TableInitializerService>();
            services.AddHostedService<AiFilterResultsConsumerService>();

            services.AddHealthChecks().AddCheck<WorkersHealthCheck>("workers");
        }

        private void AddRepository<T>(string tableName) where T : class
        {
            services.AddKeyedSingleton<TableClient>(tableName, (sp, _) =>
            {
                var factory = sp.GetRequiredService<IAzureClientFactory<TableServiceClient>>();
                var serviceClient = factory.CreateClient(TelegramSettings.TableServiceClientName);
                return serviceClient.GetTableClient(tableName);
            });
            services.AddSingleton<T>();
        }

        private void AddTelegramHandlers()
        {
            services.AddSingleton<IncomingPostHandler>();
            services.AddSingleton<UnsubscribeHandler>();
        }
    }
}
