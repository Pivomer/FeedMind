using System.Diagnostics;
using OpenTelemetry.Trace;

namespace FeedMind.Modules.Filtering;

public static class Telemetry
{
    public static readonly ActivitySource Source = new("FeedMind.Modules.Filtering");

    public static TracerProviderBuilder AddFilteringInstrumentation(this TracerProviderBuilder tracer)
    {
        tracer.AddSource(Source.Name);
        return tracer;
    }

    public static class TraceContext
    {
        public const string TraceParent = "traceparent";
        public const string TraceState = "tracestate";
    }

    public static class Operations
    {
        public const string TelegramRequestProcess = "telegram.filter.request.process";
        public const string TelegramRequestConsume = "telegram.filter.request.consume";
        public const string TelegramResultPublish = "telegram.filter.result.publish";
    }

    public static class Tags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingOperation = "messaging.operation.name";
        public const string MessagingDestinationName = "messaging.destination.name";
        public const string MessagingMessageId = "messaging.message.id";
        public const string TelegramFilterChatId = "telegram.filter.chat_id";
        public const string TelegramFilterChannelId = "telegram.filter.channel_id";
        public const string TelegramFilterDecision = "telegram.filter.decision";
    }
}
