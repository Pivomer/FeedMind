using System.Net;

namespace FeedMind.Modules.Telegram.Application.Utils;

public static class PostContentUtils
{
    private const int MaxContentLength = 800;
    private const int MinLastSpaceSearchThreshold = 40;

    public static string BuildFormattedPostText(string text, long channelId, int messageId, string linkDisplayText = "Link")
    {
        var cleanId = Math.Abs(channelId);
        if (cleanId > 1_000_000_000_000)
        {
            cleanId -= 1_000_000_000_000;
        }

        var url = $"https://t.me/c/{cleanId}/{messageId}";
        var link = $"\n<a href=\"{url}\">{linkDisplayText}</a>";

        if (text.Length <= MaxContentLength)
        {
            return text + link;
        }

        var cut = text[..MaxContentLength];
        var lastSpaceIndex = cut.LastIndexOfAny([' ', '\n', '\t', '.', ',', '!', '?']);

        var normalized = lastSpaceIndex > MinLastSpaceSearchThreshold
            ? string.Concat(cut.AsSpan(0, lastSpaceIndex), "…")
            : cut + "…";

        return normalized + link;
    }

    public static string NormalizeContent(string? rawText)
    {
        var normalised = WebUtility.HtmlEncode(rawText);
        var text = (normalised ?? string.Empty).Trim();
        return text;
    }
}
