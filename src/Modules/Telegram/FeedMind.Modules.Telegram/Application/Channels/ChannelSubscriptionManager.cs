using System.Collections.Concurrent;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram.Application.Channels;

public sealed class ChannelSubscriptionManager
{
    private readonly ConcurrentDictionary<string, object> _listeningChannels = new();

    private readonly ILogger<ChannelSubscriptionManager> _logger;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly WTelegramClient _telegramClient;

    public ChannelSubscriptionManager(ILogger<ChannelSubscriptionManager> logger, SubscriptionRepository subscriptionRepository, WTelegramClient telegramClient)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
        _telegramClient = telegramClient;
    }

    public async Task InitializeListeningChannels(CancellationToken cancellationToken)
    {
        var activeChannels = await _subscriptionRepository.GetDistinctActiveChannelNames(cancellationToken);

        foreach (var channelName in activeChannels)
        {
            _listeningChannels.TryAdd(channelName, new object());
        }

        _logger.LogInformation("Initialized listening channels: {Count}", _listeningChannels.Count);
    }

    public async Task Sync(CancellationToken cancellationToken)
    {
        var desiredChannelNames = await _subscriptionRepository.GetDistinctActiveChannelNames(cancellationToken);
        var currentlyListening = _listeningChannels.Keys.ToHashSet();
        var channelsToStart = desiredChannelNames.Except(currentlyListening).ToList();
        var channelsToStop = currentlyListening.Except(desiredChannelNames).ToList();

        _logger.LogInformation("Sync: starting {StartCount}, stopping {StopCount} channels", channelsToStart.Count, channelsToStop.Count);
        await StartListeningChannels(channelsToStart, cancellationToken);
        StopListeningChannels(channelsToStop);
    }

    private void StopListeningChannels(List<string> channelsToStop)
    {
        foreach (var channelName in channelsToStop)
        {
            try
            {
                if (_listeningChannels.TryRemove(channelName, out var state))
                {
                    //TODO Call telegram client method
                    _logger.LogInformation("Stopped listening channel {Channel}", channelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave channel {Channel}", channelName);
            }
        }
    }

    private async Task StartListeningChannels(List<string> channelsToStart, CancellationToken cancellationToken)
    {
        foreach (var channelName in channelsToStart)
        {
            try
            {
                var joinResult = await _telegramClient.JoinToChannel(channelName);
                switch (joinResult)
                {
                    case JoinChannelInfo.Success:
                    case JoinChannelInfo.AlreadyJoined:
                        _listeningChannels.TryAdd(channelName, new object());
                        _logger.LogInformation("Listening started for channel {Channel}", channelName);
                        break;

                    case JoinChannelInfo.ChannelNotFound:
                    case JoinChannelInfo.InvalidChannel:
                        _logger.LogWarning("Channel {Channel} not found or invalid, removing from subscriptions", channelName);
                        await RemoveChannel(channelName, cancellationToken);
                        break;

                    case JoinChannelInfo.RateLimited rateLimited:
                        _logger.LogWarning("Rate limited on channel {Channel}, retry after {Retry}", channelName, rateLimited.RetryAfter);
                        await RemoveChannel(channelName, cancellationToken);
                        break;

                    case JoinChannelInfo.AccessDenied:
                        _logger.LogWarning("Access denied to channel {Channel}", channelName);
                        await RemoveChannel(channelName, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join channel {Channel}", channelName);
            }
        }
    }

    private async Task RemoveChannel(string channelName, CancellationToken cancellationToken)
    {
        await _subscriptionRepository.RemoveChannel(channelName, cancellationToken);
    }
}
