using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(ILogger<MessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var text = message.Text.Trim();
        switch (text.ToLowerInvariant())
        {
            case "/start":
                await bot.SendMessage(
                    message.Chat.Id,
                    "Welcome to FeedMind Bot!",
                    cancellationToken: ct);
                break;

            case "/help":
                await bot.SendMessage(
                    message.Chat.Id,
                    "Available commands:\n" +
                    "/start — start bot\n" +
                    "/help — show this message\n" +
                    "/status — bot status (coming soon)",
                    cancellationToken: ct);
                break;
        }
    }
}
