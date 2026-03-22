using System.Buffers.Binary;

namespace FeedMind.Modules.Telegram.Infrastructure.Wclient;

internal sealed class MemorySessionStore : MemoryStream
{
    public MemorySessionStore(string sessionPath)
    {
        var file = File.ReadAllBytes(sessionPath);
        if (file.Length >= 8)
        {
            var position = BinaryPrimitives.ReadInt32LittleEndian(file);
            var length = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4));
            Write(file, position, length);
            Position = 0;
        }
    }
}
