using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.LLM;

internal sealed class LlmClient : ILlmClient
{
    private readonly IChatClient _chatClient;
    private readonly LlmApiOptions _options;
    private readonly ILogger<LlmClient> _logger;

    public LlmClient(IChatClient chatClient, IOptions<LlmApiOptions> options, ILogger<LlmClient> logger)
    {
        _chatClient = chatClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetEnrichmentJsonAsync(string prompt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Sending LLM request to {BaseUrl} with model {Model}. Prompt length: {PromptLength} chars",
            _options.BaseUrl,
            _options.Model,
            prompt.Length);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a place metadata assistant. Respond ONLY with valid JSON."),
            new(ChatRole.User, prompt)
        };

        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Json,
            ModelId = _options.Model
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, chatOptions, ct);

            if (response.Messages.Count == 0 || string.IsNullOrEmpty(response.Messages[0].Text))
                throw new InvalidOperationException("Empty LLM response");

            _logger.LogInformation("LLM response received successfully");
            return response.Messages[0].Text!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "LLM request failed. BaseUrl: {BaseUrl}, Model: {Model}, Error: {ErrorMessage}",
                _options.BaseUrl,
                _options.Model,
                ex.Message);
            throw;
        }
    }
}
