using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Filtering.Contracts;
using FeedMind.Modules.Filtering.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeedMind.Modules.Filtering.Infrastructure.ServiceBus;

public sealed class TelegramResultPublisher(ILogger<TelegramResultPublisher> logger, ServiceBusClient client, IOptions<FilteringSettings> options)
    : ServiceBusPublisherBase<TelegramFilterResult>(logger, client, options.Value.TelegramResultsQueueName)
{
    protected override string OperationName => Telemetry.Operations.TelegramResultPublish;
}
