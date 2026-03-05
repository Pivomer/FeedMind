using Azure.Messaging.ServiceBus;
using FeedMind.Modules.Filtering.Infrastructure.ServiceBus;
using FeedMind.Modules.Filtering.Services.Health;
using FeedMind.Modules.Filtering.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeedMind.Modules.Filtering.Services.Background;

public abstract class ServiceBusConsumerBase<TMessage> : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly TimeSpan _longPollingTimeSpan;
    private readonly WorkerState _health;
    private const int ServiceBusReceiveMaxSize = 150;

    protected ILogger Logger { get; }
    protected abstract string QueueName { get; }
    protected abstract Task Handle(IReadOnlyList<MessageQueueItem<TMessage>> messages, CancellationToken cancellationToken);

    protected void RecordSuccess() => _health.RecordSuccess();

    protected void RecordError() => _health.RecordError();

    protected ServiceBusConsumerBase(ILogger logger, ServiceBusClient serviceBusClient, IOptions<FilteringSettings> options, WorkerState health)
    {
        Logger = logger;
        _serviceBusClient = serviceBusClient;
        _longPollingTimeSpan = options.Value.LongPollingTimeSpan;
        _health = health;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiver = _serviceBusClient.CreateReceiver(QueueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        Logger.LogInformation("{ConsumerName} started, listening on {QueueName}", GetType().Name, QueueName);
        _health.MarkHealthy();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var received = await receiver.ReceiveMessagesAsync(ServiceBusReceiveMaxSize, _longPollingTimeSpan, stoppingToken);
                if (!received.Any())
                {
                    continue;
                }

                var group = received.Select(x => new MessageQueueItem<TMessage>(receiver, x)).ToList();

                try
                {
                    await Handle(group, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unhandled exception processing batch");
                    foreach (var item in group)
                    {
                        await item.DeadLetter("Unhandled exception", ex.Message, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("{ConsumerName} stopped gracefully", GetType().Name);
                return;
            }
            catch (Exception exception)
            {
                _health.MarkUnhealthy(exception.Message);
                Logger.LogCritical(exception, "{ConsumerName} crashed", GetType().Name);
                throw;
            }
        }
    }
}
