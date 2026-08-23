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

    public async Task<ResolvedChannel?> ResolveChannel(string channelName)
    {
        var client = await _client.Value;
        var resolved = await client.Contacts_ResolveUsername(channelName);
        return resolved.Chat is Channel channel
            ? new ResolvedChannel(new InputPeerChannel(channel.id, channel.access_hash))
            : null;
    }

    public async Task<HashSet<long>> GetJoinedChannelIds()
    {
        var client = await _client.Value;
        var dialogs = await client.Messages_GetDialogs();
        if (dialogs is not Messages_Dialogs msgDialogs)
        {
            return [];
        }

        return msgDialogs.chats.Values.OfType<Channel>().Select(channel => channel.id).ToHashSet();
    }

    public async Task<int> GetLatestMessageId(InputPeerChannel peer)
    {
        var history = await GetHistorySince(peer, minMessageId: 0, limit: 1);
        return history switch
        {
            HistoryFetchResult.Success success => success.Messages.Count == 0 ? 0 : success.Messages.Max(m => m.id),
            _ => 0
        };
    }

    public async Task<HistoryFetchResult> GetHistorySince(InputPeerChannel peer, int minMessageId, int limit)
    {
        try
        {
            var client = await _client.Value;
            var history = await client.Messages_GetHistory(peer, min_id: minMessageId, limit: limit);
            var messages = history.Messages.OfType<Message>().ToList();
            return new HistoryFetchResult.Success(messages);
        }
        catch (RpcException exception) when (exception.Code == 420)
        {
            return new HistoryFetchResult.FloodWait(TelegramErrorAnalyzer.ParseFloodWaitSeconds(exception));
        }
        catch (RpcException exception)
        {
            _logger.LogWarning(exception, "Failed to fetch history for peer {PeerId}", peer.channel_id);
            OnTransientError?.Invoke(new TelegramTransientError(exception));
            return new HistoryFetchResult.TransientFailure();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_initializedClient is null)
        {
            _logger.LogWarning("WTelegramClient DisposeAsync called but client was never initialized");
            return;
        }

        try
        {
            _logger.LogInformation("WTelegramClient start disposing");
            await _initializedClient.DisposeAsync();
            _logger.LogInformation("WTelegramClient disposed successfully");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "WTelegramClient error during dispose");
        }

        await ValueTask.CompletedTask;
    }

    private async Task<Client> CreateClient(string apiId, string apiHash, string phoneNumber, string sessionPath)
    {
        var initialBytes = File.Exists(sessionPath) ? await File.ReadAllBytesAsync(sessionPath) : [];

        while (true)
        {
            try
            {
                var client = new Client(what =>
                    {
                        return what switch
                        {
                            "api_id" => apiId,
                            "api_hash" => apiHash,
                            "phone_number" => phoneNumber,
                            "verification_code" => GetVerificationCode(),
                            _ => null
                        };
                    },
                    startSession: initialBytes,
                    saveSession: bytes => File.WriteAllBytes(sessionPath, bytes)
                );

                await client.LoginUserIfNeeded();
                return client;
            }
            catch (RpcException exception) when (exception.Code == 420)
            {
                var waitSeconds = TelegramErrorAnalyzer.ParseFloodWaitSeconds(exception) + TelegramErrorAnalyzer.FloodWaitBufferSeconds;
                _logger.LogCritical("FLOOD_WAIT {Seconds}s during login — waiting full duration", waitSeconds);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
            }
        }
    }

    private string GetVerificationCode()
    {
        var code = Environment.GetEnvironmentVariable("TELEGRAM_VERIFICATION_CODE");
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogCritical("TELEGRAM_VERIFICATION_CODE not set. Application suspended, waiting for manual intervention");
            Thread.Sleep(Timeout.Infinite);
            throw new InvalidOperationException("Verification code not found in environment variables");
        }

        return code;
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

    public async Task MarkChannelAsRead(InputPeerChannel peer)
    {
        try
        {
            var client = await _client.Value;
            await client.Channels_ReadHistory(peer);
        }
        catch (RpcException exception)
        {
            _logger.LogWarning(exception, "Failed to mark channel {ChannelId} history as read", peer.channel_id);
            OnTransientError?.Invoke(new TelegramTransientError(exception));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to mark channel {ChannelId} history as read", peer.channel_id);
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
