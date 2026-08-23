using TL;

namespace FeedMind.Modules.Telegram.Domain.Models;

public sealed record ResolvedChannel(InputPeerChannel Peer)
{
    public long ChannelId => Peer.channel_id;
}
