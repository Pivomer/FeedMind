using System.Collections.Concurrent;
using Azure.Security.KeyVault.Secrets;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Wclient.Handlers;
using FeedMind.Modules.Telegram.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using WTelegram;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

public sealed record TelegramTransientError(Exception Exception);
public sealed record TelegramFatalError(Exception Exception);

public sealed class WTelegramClient : IAsyncDisposable
{
    private readonly ILogger<WTelegramClient> _logger;
    private readonly JoinChannelErrorHandler _joinChannelErrorHandler;
    private readonly Lazy<Task<Client>> _client;
    private Client? _initializedClient;
    private bool _subscribed;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly ConcurrentDictionary<long, InputPeerChannel> _channelPeers = new();

    public event Func<Message, Task>? OnMessageReceived;
    public event Action<TelegramTransientError>? OnTransientError;
    public event Action<TelegramFatalError>? OnFatalError;

    public WTelegramClient(
        ILogger<WTelegramClient> logger,
        IOptions<TelegramSettings> options,
        SecretClient secretClient,
        SessionManager sessionManager,
        JoinChannelErrorHandler joinChannelErrorHandler)
    {
        _logger = logger;
        _joinChannelErrorHandler = joinChannelErrorHandler;
        var settings = options.Value;
        _client = InitClient(secretClient, sessionManager, settings);
    }

    private Lazy<Task<Client>> InitClient(SecretClient secretClient, SessionManager sessionManager, TelegramSettings settings)
    {
        return new Lazy<Task<Client>>(async () =>
        {
            try
            {
                var apiId = secretClient.GetSecret(settings.ApiId).Value.Value;
                var apiHash = secretClient.GetSecret(settings.ApiHash).Value.Value;
                var phoneNumber = secretClient.GetSecret(settings.PhoneNumber).Value.Value;
                var session = sessionManager.GetSessionPath();

                var client = await CreateClient(apiId, apiHash, phoneNumber, session);
                _initializedClient = client;
                return client;
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

    public async Task<JoinChannelInfo> JoinToChannel(string channelName)
    {
        await _channelLock.WaitAsync();
        try
        {
            var client = await _client.Value;
            var resolved = await client.Contacts_ResolveUsername(channelName);
            if (resolved.Chat is not Channel channel)
            {
                return new JoinChannelInfo.ChannelNotFound();
            }

            await client.Channels_JoinChannel(channel);
            _channelPeers[channel.id] = new InputPeerChannel(channel.id, channel.access_hash);
            return new JoinChannelInfo.Success();
        }
        catch (RpcException exception)
        {
            return _joinChannelErrorHandler.HandleRpcException(exception, channelName, OnTransientError);
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Failed to join channel");
            throw;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_initializedClient is null)
        {
            return;
        }

        try
        {
            await _initializedClient.DisposeAsync();
            _logger.LogInformation("WTelegram Client disposed, session file released");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error disposing WTelegram Client");
        }

        await ValueTask.CompletedTask;
    }

    private static async Task<Client> CreateClient(string apiId, string apiHash, string phoneNumber, string sessionPath)
    {
        var sessionStore = new MemorySessionStore(sessionPath);
        var client = new Client(what =>
        {
            return what switch
            {
                "api_id" => apiId,
                "api_hash" => apiHash,
                "phone_number" => phoneNumber,
                _ => null
            };
        }, sessionStore);

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

        if (message.Peer is not PeerChannel peerChannel)
        {
            return;
        }

        await MarkChannelHistoryAsRead(peerChannel);

        if (OnMessageReceived == null)
        {
            _logger.LogWarning("Message received but no subscribers registered");
            return;
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

    private async Task MarkChannelHistoryAsRead(PeerChannel peerChannel)
    {
        try
        {
            var client = await _client.Value;

            if (_channelPeers.TryGetValue(peerChannel.ID, out var savedPeer))
            {
                await client.Channels_ReadHistory(savedPeer);
                return;
            }

            var dialogs = await client.Messages_GetDialogs();
            if (dialogs is not Messages_Dialogs msgDialogs)
            {
                return;
            }

            if (msgDialogs.chats.TryGetValue(peerChannel.ID, out var chat) == false)
            {
                return;
            }

            if (chat is not Channel channel)
            {
                return;
            }

            var inputPeer = new InputPeerChannel(channel.id, channel.access_hash);
            await client.Channels_ReadHistory(inputPeer);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to mark channel {ChannelId} history as read", peerChannel.ID);
            OnTransientError?.Invoke(new TelegramTransientError(ex));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to mark channel {ChannelId} history as read", peerChannel.ID);
        }
    }
}
