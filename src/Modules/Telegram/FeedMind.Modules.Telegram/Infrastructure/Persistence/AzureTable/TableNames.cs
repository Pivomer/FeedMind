using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable;

public static class TableNames
{
    public static string[] GetAllTableNames() =>
    [
        SubscriptionRepository.TableName,
        UserRepository.TableName,
        MessageRepository.TableName
    ];
}
