namespace FeedMind.Modules.Telegram.Application.Utils;

public static class PostContentUtils
{
    private const int MaxContentLength = 800;
    private const int MinLastSpaceSearchThreshold = 40;

    public static string BuildFormattedPostText(string? rawContent, long channelId, int messageId, string linkDisplayText = "Link")
    {
        var normalized = NormalizeContent(rawContent ?? string.Empty);

        var cleanId = Math.Abs(channelId);
        if (cleanId > 1_000_000_000_000)
        {
            cleanId -= 1_000_000_000_000;
        }

        var url = $"https://t.me/c/{cleanId}/{messageId}";
        return $"{normalized}\n<a href=\"{url}\">{linkDisplayText}</a>";
    }

    public static string NormalizeContent(string? rawText)
    {
        var text = (rawText ?? string.Empty).Trim();
        if (text.Length <= MaxContentLength)
        {
            return text;
        }

        var cut = text[..MaxContentLength];
        var lastSpaceIndex = cut.LastIndexOfAny([' ', '\n', '\t', '.', ',', '!', '?']);

        return lastSpaceIndex > MinLastSpaceSearchThreshold
            ? string.Concat(cut.AsSpan(0, lastSpaceIndex), "…")
            : cut + "…";
    }
}
