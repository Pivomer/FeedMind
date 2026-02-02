using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class BotUpdateHandler
{
    private readonly ILogger<BotUpdateHandler> _logger;
    private readonly MessageHandler _messageHandler;

    public BotUpdateHandler(ILogger<BotUpdateHandler> logger, MessageHandler messageHandler)
    {
        _logger = logger;
        _messageHandler = messageHandler;
    }

    public async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message when update.Message is { } message:
                    await _messageHandler.HandleMessageAsync(bot, message, cancellationToken);
                    break;
                case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
                    await bot.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "Feature not implemented yet",
                        showAlert: false,
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process update of type {UpdateType}", update.Type);
        }
    }
}
