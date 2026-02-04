using System.Threading.Channels;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using FeedMind.Modules.Telegram.Application.Handlers.Commands;
using FeedMind.Modules.Telegram.Application.Handlers.Posts;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using FeedMind.Modules.Telegram.Infrastructure.BotApi;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
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
        public void AddTelegramModule(IConfigurationSection section, string appHome)
        {
            services.AddOptions<TelegramSettings>()
                .Bind(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddChannels();
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
            services.AddScoped<BotApiClient>();

            services.AddSingleton<WTelegramClient>();
            services.AddScoped<BotUpdateHandler>();
            services.AddScoped<MessageHandler>();

            services.AddTelegramHandlers();
            services.AddRepository<UserRepository>(UserRepository.TableName);
            services.AddRepository<SubscriptionRepository>(SubscriptionRepository.TableName);

            services.AddHostedService<ChannelFeedListenerService>();
            services.AddHostedService<BotPollingService>();
            services.AddHostedService<PostConsumerService>();
            services.AddHostedService<TableInitializerService>();

            services.AddSingleton<WorkerStates>();
            services.AddHealthChecks().AddCheck<WorkersHealthCheck>("workers");
        }

        private void AddChannels()
        {
            AddChannel<TelegramPost>();
            AddChannel<RawTelegramMessageDto>();
            return;

            void AddChannel<T>()
            {
                var channel = Channel.CreateUnbounded<T>();
                services.AddSingleton(channel.Reader);
                services.AddSingleton(channel.Writer);
            }
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
