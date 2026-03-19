using System.Text;
using FeedMind.Modules.Telegram.Contracts;

namespace FeedMind.Modules.Filtering.Application.Telegram;

public static class TelegramFilterPromptBuilder
{
    private const int MaxExamples = 15;

    private const string SystemPrompt =
        """
        You are a news filter. Decide whether to show a post to the user or not.

        Use the like/dislike history to understand user preferences.
        If there are few ratings — lean towards show.

        Reply ONLY with a JSON object, no markdown, no text outside JSON:
        {"show": true, "reason": "brief reason"}
        """;

    public static string System => SystemPrompt;

    public static string Build(TelegramFilterRequest request)
    {
        var liked = request.LikedTexts.TakeLast(MaxExamples).ToList();
        var disliked = request.DislikedTexts.TakeLast(MaxExamples).ToList();

        var builder = new StringBuilder();

        builder.AppendLine($"User has {liked.Count} likes and {disliked.Count} dislikes for this channel.");
        builder.AppendLine();

        builder.AppendLine("<liked>");
        if (liked.Count == 0)
        {
            builder.AppendLine("none");
        }
        else
        {
            for (var i = 0; i < liked.Count; i++)
            {
                builder.AppendLine($"{i + 1}. {liked[i]}");
            }
        }

        builder.AppendLine("</liked>");
        builder.AppendLine();

        builder.AppendLine("<disliked>");
        if (disliked.Count == 0)
        {
            builder.AppendLine("none");
        }
        else
        {
            for (var i = 0; i < disliked.Count; i++)
            {
                builder.AppendLine($"{i + 1}. {disliked[i]}");
            }
        }

        builder.AppendLine("</disliked>");
        builder.AppendLine();

        builder.AppendLine("<post>");
        builder.AppendLine(request.Text);
        builder.Append("</post>");

        return builder.ToString();
    }
}
