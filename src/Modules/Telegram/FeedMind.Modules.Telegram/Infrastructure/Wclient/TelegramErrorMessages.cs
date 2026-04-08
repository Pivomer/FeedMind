namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

public static class TelegramErrorMessages
{
    //common
    public const string ChatInvalid = "CHAT_INVALID";
    public const string MsgIdInvalid = "MSG_ID_INVALID";
    public const string ChannelInvalid = "CHANNEL_INVALID";
    public const string ChannelPrivate= "CHANNEL_PRIVATE";
    public const string FloodWait = "FLOOD_WAIT";

    //channels.leaveChannel
    public const string ChannelPublicGroupNa = "CHANNEL_PUBLIC_GROUP_NA";
    public const string UserBannedInChannel = "USER_BANNED_IN_CHANNEL";
    public const string UserCreator = "USER_CREATOR";
    public const string UserNotParticipant = "USER_NOT_PARTICIPANT";

    //channels.joinChannel
    public const string ChannelsTooMuch = "CHANNELS_TOO_MUCH";
    public const string ChannelMonoForumUnsupported = "CHANNEL_MONOFORUM_UNSUPPORTED";
    public const string FrozenMethodInvalid = "FROZEN_METHOD_INVALID";
    public const string InviteHashEmpty = "INVITE_HASH_EMPTY";
    public const string InviteHashExpired = "INVITE_HASH_EXPIRED";
    public const string InviteHashInvalid = "INVITE_HASH_INVALID";
    public const string InviteRequestSent = "INVITE_REQUEST_SENT";
    public const string PeerIdInvalid = "PEER_ID_INVALID";
    public const string UsersTooMuch = "USERS_TOO_MUCH";
    public const string UserAlreadyParticipant = "USER_ALREADY_PARTICIPANT";
    public const string UserChannelsTooMuch = "USER_CHANNELS_TOO_MUCH";
}
