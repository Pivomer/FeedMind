using Azure.Security.KeyVault.Secrets;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using WTelegram;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

public sealed record TelegramTransientError(Exception Exception);
public sealed record TelegramFatalError(Exception Exception);

public sealed class WTelegramClient
{
    private readonly ILogger<WTelegramClient> _logger;
    private readonly Lazy<Task<Client>> _client;
    private bool _subscribed;

    public event Func<Message, Task>? OnMessageReceived;
    public event Action<TelegramTransientError>? OnTransientError;
    public event Action<TelegramFatalError>? OnFatalError;

    public WTelegramClient(ILogger<WTelegramClient> logger, IOptions<TelegramSettings> options, SecretClient secretClient, SessionManager sessionManager)
    {
        _logger = logger;
        var settings = options.Value;
        _client = InitClient(secretClient, sessionManager, settings);
    }

    private static Lazy<Task<Client>> InitClient(SecretClient secretClient, SessionManager sessionManager, TelegramSettings settings)
    {
        return new Lazy<Task<Client>>(async () =>
        {
            try
            {
                var apiId = secretClient.GetSecret(settings.ApiId).Value.Value;
                var apiHash = secretClient.GetSecret(settings.ApiHash).Value.Value;
                var phoneNumber = secretClient.GetSecret(settings.PhoneNumber).Value.Value;
                var session = sessionManager.GetSessionPath();

                return await CreateClient(apiId, apiHash, phoneNumber, session);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Telegram client initialization failed", exception);
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task SubscribeToUpdates()
    {
        if (_subscribed)
        {
            _logger.LogWarning("Telegram updates already subscribed");
            return;
        }

        try
        {
            var client = await _client.Value;
            _logger.LogInformation("Subscribing to Telegram updates");
            client.WithUpdateManager(ClientOnUpdate);
            _subscribed = true;
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Failed to subscribe Telegram updates");
            OnFatalError?.Invoke(new TelegramFatalError(exception));
            throw;
        }
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

    private async Task ClientOnUpdate(Update update)
    {
        try
        {
            if (update is UpdateNewChannelMessage unm)
            {
                await HandleMessage(unm.message);
            }
        }
        catch (RpcException exception)
        {
            _logger.LogWarning(exception, "Telegram RPC error");
            OnTransientError?.Invoke(new TelegramTransientError(exception));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Telegram fatal update error");
            OnFatalError?.Invoke(new TelegramFatalError(exception));
        }
    }

    private async Task HandleMessage(MessageBase messageBase)
    {
        if (messageBase is not Message message)
        {
            return;
        }

        if (message.Peer is not PeerChannel)
        {
            return;
        }

        if (OnMessageReceived == null)
        {
            throw new InvalidOperationException("No subscribers for OnMessageReceived event");
        }

        try
        {
            await OnMessageReceived.Invoke(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error in OnMessageReceived handler");
        }
    }
}
