namespace FeedMind.Modules.Telegram.Domain.Models;

public abstract record JoinChannelInfo
{
    public sealed record Success : JoinChannelInfo;

    public sealed record ChannelNotFound : JoinChannelInfo;

    public sealed record AlreadyJoined : JoinChannelInfo;

    public sealed record AccessDenied : JoinChannelInfo;

    public sealed record InvalidChannel : JoinChannelInfo;

    public sealed record FloodWait(int WaitSeconds) : JoinChannelInfo;

    public sealed record InviteRequestSent : JoinChannelInfo;

    public sealed record AccountLimitExceeded : JoinChannelInfo;

    public sealed record TransientFailure : JoinChannelInfo;
}
