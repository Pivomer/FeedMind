using System.Diagnostics;
using OpenTelemetry.Trace;

namespace FeedMind.Modules.Telegram;

public static class Telemetry
{
    public static readonly ActivitySource Source = new("FeedMind.Modules.Telegram");

    public static TracerProviderBuilder AddTelegramInstrumentation(this TracerProviderBuilder tracer)
    {
        tracer.AddHttpClientInstrumentation(x =>
        {
            x.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                if (request.RequestUri?.Host == "api.telegram.org")
                {
                    activity.SetTag("url.full", "https://api.telegram.org/***");
                }
            };
        });
        tracer.AddSource(Source.Name);
        return tracer;
    }

    public static class Operations
    {
        public const string TelegramMessageReceive = "telegram.message.receive";
        public const string InternalMessageProcess = "internal.message.process";
        public const string MessageHandle = "message.handle";
    }

    public static class Tags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingOperation = "messaging.operation.name";
        public const string MessagingDestinationName = "messaging.destination.name";
        public const string MessagingMessageId = "messaging.message.id";

        public const string TelegramChannelId = "messaging.telegram.channel_id";
        public const string TelegramChatId = "messaging.telegram.chat_id";
        public const string TelegramCommandName = "messaging.telegram.command_name";
        public const string TelegramMessageType = "messaging.telegram.message_type";
    }
}
