using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using FeedMind.Modules.Filtering.Application.Telegram;
using FeedMind.Modules.Filtering.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace FeedMind.Modules.Filtering.Application;

public sealed class OpenAiFilterClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiFilterClient> _logger;

    public OpenAiFilterClient(AzureOpenAIClient azureOpenAiClient, IOptions<FilteringSettings> options, ILogger<OpenAiFilterClient> logger)
    {
        _logger = logger;
        _chatClient = azureOpenAiClient.GetChatClient(options.Value.OpenAiDeploymentName);
    }

    public async Task<FilterResult> Filter(string userPrompt, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(TelegramFilterPromptBuilder.System),
            new UserChatMessage(userPrompt)
        };

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            var content = completion.Value.Content[0].Text;
            return Parse(content);
        }
        catch (ClientResultException ex) when (ex.Message.Contains("content_filter"))
        {
            _logger.LogWarning("Content filter triggered, defaulting to show. Prompt length: {Length}", userPrompt.Length);
            return new FilterResult(true, "content-filter");
        }
    }

    private FilterResult Parse(string content)
    {
        try
        {
            var document = JsonDocument.Parse(content);
            var show = document.RootElement.GetProperty("show").GetBoolean();
            var reason = document.RootElement.GetProperty("reason").GetString() ?? string.Empty;
            return new FilterResult(show, reason);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to parse OpenAI response, defaulting to show. Response: {Content}", content);
            return new FilterResult(true, "parse-error");
        }
    }
}
