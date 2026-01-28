using FeedMind.Modules.Telegram.API.WTelegram;
using Microsoft.Extensions.Logging;

namespace FeedMind.Modules.Telegram;

public sealed class Job
{
    private readonly ILogger<Job> _logger;
    private readonly WTelegramService _wTelegramService;

    public Job(ILogger<Job> logger, WTelegramService wTelegramService)
    {
        _logger = logger;
        _wTelegramService = wTelegramService;
    }

    public async Task Run(CancellationToken stoppingToken)
    {
        var posts = await _wTelegramService.GetNewPosts(stoppingToken);
        _logger.LogInformation("Loaded {count} posets", posts.Count);
    }
}
