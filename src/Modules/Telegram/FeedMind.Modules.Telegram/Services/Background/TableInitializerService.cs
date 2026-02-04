using Azure.Data.Tables;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Services.Background;

public sealed class TableInitializerService : BackgroundService
{
    private readonly ILogger<TableInitializerService> _logger;
    private readonly TableServiceClient _serviceClient;

    public TableInitializerService(ILogger<TableInitializerService> logger, IAzureClientFactory<TableServiceClient> clientFactory)
    {
        _logger = logger;
        _serviceClient = clientFactory.CreateClient(TelegramSettings.TableServiceClientName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TableInitializerService is starting...");
        var tableNames = TableNames.GetAllTableNames().ToList();

        foreach (var tableName in tableNames)
        {
            try
            {
                await _serviceClient.CreateTableIfNotExistsAsync(tableName, stoppingToken);
                _logger.LogInformation("Table {TableName} is ready", tableName);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to ensure table {TableName} exists", tableName);
                throw;
            }
        }

        _logger.LogInformation("Table initialization completed successfully. {Count} tables checked.", tableNames.Count);
    }
}
