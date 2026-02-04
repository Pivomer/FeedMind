using Azure;
using Azure.Data.Tables;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;

public sealed class UserRepository
{
    public const string TableName = "FeedMindTelegramUsers";

    private readonly TableClient _tableClient;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository([FromKeyedServices(TableName)] TableClient tableClient, ILogger<UserRepository> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task RegisterOrGetUser(string chatId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _tableClient.GetEntityAsync<UserEntity>(UserEntity.PartitionKeyValue, chatId, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            var newUser = new UserEntity
            {
                RowKey = chatId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastInteraction = null
            };

            try
            {
                await _tableClient.AddEntityAsync(newUser, cancellationToken);
                _logger.LogInformation("Registered new user ChatId {ChatId}", chatId);
            }
            catch (RequestFailedException failedException) when (failedException.Status == 409)
            {
                await _tableClient.GetEntityAsync<UserEntity>(UserEntity.PartitionKeyValue, chatId, cancellationToken: cancellationToken);
                _logger.LogWarning("Race condition: user already registered ChatId {ChatId}", chatId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to register or get user ChatId {ChatId}", chatId);
            throw;
        }
    }

    public async Task UpdateLastInteraction(string chatId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<UserEntity>(UserEntity.PartitionKeyValue, chatId, cancellationToken: cancellationToken);
            var existingUser = entity.Value;
            existingUser.LastInteraction = DateTimeOffset.UtcNow;

            await _tableClient.UpdateEntityAsync(
                existingUser,
                existingUser.ETag,
                TableUpdateMode.Merge,
                cancellationToken);
        }
        catch (RequestFailedException failedException) when (failedException.Status == 404)
        {
            _logger.LogWarning("UpdateLastInteraction failed: user not found ChatId {ChatId}", chatId);
        }
        catch (RequestFailedException failedException) when (failedException.Status == 412)
        {
            _logger.LogWarning("ETag conflict while updating last interaction ChatId {ChatId}", chatId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update last interaction for ChatId {ChatId}", chatId);
            throw;
        }
    }
}
