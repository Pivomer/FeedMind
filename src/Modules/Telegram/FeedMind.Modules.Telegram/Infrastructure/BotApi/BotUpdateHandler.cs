using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FeedMind.Modules.Telegram.Infrastructure.BotApi;

public sealed class BotUpdateHandler
{
    private readonly ILogger<BotUpdateHandler> _logger;
    private readonly MessageHandler _messageHandler;
    private readonly CallbackHandler _callbackHandler;

    public BotUpdateHandler(ILogger<BotUpdateHandler> logger, MessageHandler messageHandler, CallbackHandler callbackHandler)
    {
        _logger = logger;
        _messageHandler = messageHandler;
        _callbackHandler = callbackHandler;
    }

    public async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message when update.Message is { } message:
                    await _messageHandler.Handle(bot, message, cancellationToken);
                    break;
                case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
                    await _callbackHandler.Handle(bot, callback, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unhandled update type {UpdateType}", update.Type);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process update of type {UpdateType}", update.Type);
            throw;
        }
    }
}
