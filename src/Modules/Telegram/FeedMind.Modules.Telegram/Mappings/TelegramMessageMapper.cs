using FeedMind.Modules.Telegram.Domain.Models;
using FeedMind.Modules.Telegram.DTOs.Incoming;
using Riok.Mapperly.Abstractions;
using TL;

namespace FeedMind.Modules.Telegram.Mappings;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TelegramMessageMapper
{
    [MapProperty(nameof(Message.id), nameof(RawTelegramMessageDto.MessageId))]
    [MapProperty(nameof(Message.message), nameof(RawTelegramMessageDto.Text))]
    [MapProperty(nameof(Message.peer_id), nameof(RawTelegramMessageDto.ChannelId), Use = nameof(PeerToChatId))]

    public partial RawTelegramMessageDto ToRawMessageDto(Message message);

    public static TelegramPost ToTelegramPost(RawTelegramMessageDto dto) => TelegramPost.FromRaw(dto);

    private static long PeerToChatId(Peer? peer)
    {
        return peer switch
        {
            PeerUser pu => pu.user_id,
            PeerChat pc => -pc.chat_id,
            PeerChannel pch => -1000000000000 - pch.channel_id,
            _ => 0
        };
    }
}
