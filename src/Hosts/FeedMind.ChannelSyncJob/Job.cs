using FeedMind.ChannelSyncJob.Settings;
using FeedMind.Modules.Telegram.Application.Posts;
using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.Infrastructure.Persistence.AzureTable.Repositories;
using FeedMind.Modules.Telegram.Infrastructure.Wclient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramMessageMapper = FeedMind.Modules.Telegram.Mappings.TelegramMessageMapper;

namespace FeedMind.ChannelSyncJob;

public sealed class Job
{
    private readonly ILogger<Job> _logger;
    private readonly int _historyPageLimit;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly ChannelCheckpointRepository _checkpointRepository;
    private readonly WTelegramClient _telegramClient;
    private readonly TelegramMessageMapper _mapper;
    private readonly TelegramPostDispatcher _postDispatcher;

    public Job(
        ILogger<Job> logger,
        IOptions<AppSettings> appOptions,
        SubscriptionRepository subscriptionRepository,
        ChannelCheckpointRepository checkpointRepository,
        WTelegramClient telegramClient,
        TelegramMessageMapper mapper,
        TelegramPostDispatcher postDispatcher)
    {
        _logger = logger;
        _historyPageLimit = appOptions.Value.HistoryPageLimit;
        _subscriptionRepository = subscriptionRepository;
        _checkpointRepository = checkpointRepository;
        _telegramClient = telegramClient;
        _mapper = mapper;
        _postDispatcher = postDispatcher;
    }

    public async Task<ExecutionResult> Run(CancellationToken cancellationToken)
    {
        try
        {
            var joinedChannelIds = await _telegramClient.GetJoinedChannelIds();

            var channelNames = await _subscriptionRepository.GetDistinctActiveChannelNames(cancellationToken);
            _logger.LogInformation("Channel sync job: syncing {Count} channels", channelNames.Count);

            var failures = 0;
            foreach (var channelName in channelNames)
            {
                if (!await EnsureJoinedAndSync(channelName, joinedChannelIds, cancellationToken))
                {
                    failures++;
                }
            }

            return failures == 0 ? ExecutionResult.Success() : ExecutionResult.Failure();
        }
        finally
        {
            await _telegramClient.DisposeAsync();
        }
    }

    private async Task<bool> EnsureJoinedAndSync(string channelName, HashSet<long> joinedChannelIds, CancellationToken cancellationToken)
    {
        ResolvedChannel? resolved;
        try
        {
            resolved = await _telegramClient.ResolveChannel(channelName);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to resolve channel {Channel}, skipping this run", channelName);
            return true;
        }

        if (resolved is null)
        {
            _logger.LogWarning("Channel {Channel} could not be resolved, skipping this run", channelName);
            return true;
        }

        if (!joinedChannelIds.Contains(resolved.ChannelId))
        {
            _logger.LogInformation("Bot is not a participant of {Channel}, joining", channelName);
            var joinResult = await _telegramClient.JoinToChannel(channelName);
            if (joinResult is not JoinChannelInfo.Success)
            {
                _logger.LogWarning("Failed to join channel {Channel}: {Result}", channelName, joinResult.GetType().Name);
                return true;
            }
            joinedChannelIds.Add(resolved.ChannelId);
        }

        return await SyncChannel(channelName, resolved, cancellationToken);
    }

    private async Task<bool> SyncChannel(string channelName, ResolvedChannel resolved, CancellationToken cancellationToken)
    {
        var checkpoint = await _checkpointRepository.GetCheckpoint(channelName, cancellationToken);

        if (checkpoint == 0)
        {
            var latestId = await _telegramClient.GetLatestMessageId(resolved.Peer);
            await _checkpointRepository.SaveCheckpoint(channelName, latestId, cancellationToken);
            _logger.LogInformation("Established baseline checkpoint {MessageId} for new channel {Channel}", latestId, channelName);
            return true;
        }

        var history = await _telegramClient.GetHistorySince(resolved.Peer, checkpoint, _historyPageLimit);
        switch (history)
        {
            case HistoryFetchResult.FloodWait floodWait:
                _logger.LogWarning("FLOOD_WAIT {Seconds}s fetching history for {Channel}, skipping this run", floodWait.WaitSeconds, channelName);
                return true;
            case HistoryFetchResult.TransientFailure:
                _logger.LogWarning("Transient failure fetching history for {Channel}, skipping this run", channelName);
                return true;
        }

        var success = (HistoryFetchResult.Success)history;

        foreach (var message in success.Messages.OrderBy(m => m.id))
        {
            var dto = _mapper.ToRawMessageDto(message);
            var post = TelegramMessageMapper.ToTelegramPost(dto);

            if (post is not null)
            {
                try
                {
                    await _postDispatcher.Dispatch(post, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Dispatch failed for channel {Channel} message {MessageId}; stopping this channel for this run", channelName, dto.MessageId);
                    return false;
                }
            }

            await _checkpointRepository.SaveCheckpoint(channelName, dto.MessageId, cancellationToken);
        }

        if (success.Messages.Count > 0)
        {
            await _telegramClient.MarkChannelAsRead(resolved.Peer);
        }

        return true;
    }
}
