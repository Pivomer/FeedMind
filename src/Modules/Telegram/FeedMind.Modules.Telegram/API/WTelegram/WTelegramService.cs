using Azure.Security.KeyVault.Secrets;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using WTelegram;

namespace FeedMind.Modules.Telegram.API.WTelegram;

public sealed class WTelegramService
{
    private readonly ILogger<WTelegramService> _logger;
    private readonly Lazy<Task<Client>> _client;

    public WTelegramService(
        ILogger<WTelegramService> logger,
        IOptions<TelegramSettings> options,
        SecretClient secretClient,
        SessionManager sessionManager)
    {
        _logger = logger;
        var settings = options.Value;
        _client = new Lazy<Task<Client>>(async () =>
        {
            var apiId = secretClient.GetSecret(settings.ApiId).Value.Value;
            var apiHash = secretClient.GetSecret(settings.ApiHash).Value.Value;
            var phoneNumber = secretClient.GetSecret(settings.PhoneNumber).Value.Value;
            var session = sessionManager.GetSessionPath();

            return await CreateClient(apiId, apiHash, phoneNumber, session);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static async Task<Client> CreateClient(string apiId, string apiHash, string phoneNumber, string? sessionPath)
    {
        var client = new Client(what =>
        {
            return what switch
            {
                "api_id" => apiId,
                "api_hash" => apiHash,
                "phone_number" => phoneNumber,
                "session_pathname" => sessionPath,
                _ => null
            };
        });

        await client.LoginUserIfNeeded();
        return client;
    }

    private async Task<Client> GetClient()
    {
        return await _client.Value;
    }

    public async Task<List<TelegramPost>> GetNewPosts(CancellationToken stoppingToken)
    {
        var client = await GetClient();
        var allPosts = new List<TelegramPost>();
        var messagesChats = await client.Messages_GetAllChats();

        foreach (var chatBase in messagesChats.chats.Values.Take(5))
        {
            try
            {
                var posts = await GetChannelPosts(chatBase);
                allPosts.AddRange(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get posts from @{Channel}", chatBase.Title);
            }
        }

        return allPosts;
    }

    private async Task<List<TelegramPost>> GetChannelPosts(ChatBase channelChat)
    {
        var client = await GetClient();

        _logger.LogInformation("Fetching posts from @{Channel}", channelChat.Title);

        var messages = await client.Messages_GetHistory(channelChat, limit: 5);
        var posts = new List<TelegramPost>();

        foreach (var msgBase in messages.Messages)
        {
            if (msgBase is not Message msg || string.IsNullOrWhiteSpace(msg.message))
                continue;

            var post = new TelegramPost
            {
                ChannelId = channelChat.ID.ToString(),
                ChannelUsername = channelChat.MainUsername,
                MessageId = msg.ID,
                Text = msg.message,
                Date = msg.Date,
            };

            posts.Add(post);
        }

        _logger.LogInformation("Fetched {Count} posts from @{Channel}", posts.Count, channelChat.MainUsername);

        return posts;
    }
}
