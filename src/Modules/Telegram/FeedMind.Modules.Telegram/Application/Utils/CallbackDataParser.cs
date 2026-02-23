using FeedMind.Modules.Telegram.Domain.Models;

namespace FeedMind.Modules.Telegram.Application.Utils;

public static class CallbackDataParser
{
    public static CallbackData? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var span = text.AsSpan();
        var separatorIndex = span.IndexOf(':');
        if (separatorIndex < 0)
        {
            return null;
        }

        if (!int.TryParse(span[..separatorIndex], out var typeInt))
        {
            return null;
        }

        if (!Enum.IsDefined(typeof(CallbackQueryType), typeInt))
        {
            return null;
        }

        var type = (CallbackQueryType)typeInt;
        var body = span[(separatorIndex + 1)..];

        return type switch
        {
            CallbackQueryType.Feedback => Feedback.Parse(body),
            _ => null
        };
    }

    public static class Feedback
    {
        private const string LikeAction = "like";
        private const string DislikeAction = "dislike";

        public static string BuildLike(int messageId) => $"{(int)CallbackQueryType.Feedback}:{LikeAction}:{messageId}";
        public static string BuildDislike(int messageId) => $"{(int)CallbackQueryType.Feedback}:{DislikeAction}:{messageId}";

        public static CallbackData? Parse(ReadOnlySpan<char> body)
        {
            var separatorIndex = body.IndexOf(':');
            if (separatorIndex < 0)
            {
                return null;
            }

            var action = body[..separatorIndex];
            if (!int.TryParse(body[(separatorIndex + 1)..], out var messageId))
            {
                return null;
            }

            return action switch
            {
                LikeAction => new CallbackData.Feedback(messageId, MessageFeedback.Like),
                DislikeAction => new CallbackData.Feedback(messageId, MessageFeedback.Dislike),
                _ => null
            };
        }
    }

    private enum CallbackQueryType
    {
        Feedback = 1,
    }
}
