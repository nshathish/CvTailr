using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using CvTailr.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CvTailr.Api.Clients;

/// <summary>
/// Thin wrapper around Azure AI Foundry chat-completion inference. Contains no business
/// logic — every caller supplies its own system prompt, input, and deployment name.
/// </summary>
public class FoundryClient : IFoundryClient
{
    private const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AzureOpenAIClient _client;
    private readonly ILogger<FoundryClient> _logger;

    public FoundryClient(IOptions<FoundryOptions> options, ILogger<FoundryClient> logger)
    {
        _logger = logger;

        var foundry = options.Value;
        if (string.IsNullOrWhiteSpace(foundry.Endpoint))
        {
            throw new InvalidOperationException(
                "Foundry:Endpoint is not configured. Set it via dotnet user-secrets (see README.md).");
        }

        var endpoint = new Uri(foundry.Endpoint);
        _client = string.IsNullOrWhiteSpace(foundry.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(foundry.ApiKey));
    }

    public async Task<TResult> GetStructuredCompletionAsync<TResult>(
        string systemPrompt,
        string userInput,
        string deploymentName,
        CancellationToken cancellationToken = default)
    {
        var chatClient = _client.GetChatClient(deploymentName);

        ChatMessage[] messages =
        [
            new SystemChatMessage(
                systemPrompt +
                "\n\nRespond with a single JSON object only. Do not include markdown code fences, " +
                "explanations, or any text outside the JSON object."),
            new UserChatMessage(userInput)
        ];

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var completion = await chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var json = completion.Value.Content[0].Text;

                var result = JsonSerializer.Deserialize<TResult>(json, JsonOptions);
                if (result is null)
                {
                    throw new InvalidOperationException(
                        $"Foundry deployment '{deploymentName}' returned a JSON null result.");
                }

                return result;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Transient failure calling Foundry deployment {Deployment} (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}.",
                    deploymentName, attempt, MaxAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Foundry completion against deployment '{deploymentName}' failed after {MaxAttempts} attempts.",
            lastException);
    }

    private static bool IsTransient(Exception ex) =>
        ex is ClientResultException { Status: 429 or >= 500 } or TimeoutException or HttpRequestException;
}
