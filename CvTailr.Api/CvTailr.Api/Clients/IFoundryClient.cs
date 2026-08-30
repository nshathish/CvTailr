namespace CvTailr.Api.Clients;

public interface IFoundryClient
{
    /// <summary>
    /// Sends a prompt to the given Azure AI Foundry deployment, instructing it to respond with
    /// JSON only, and deserializes the response into <typeparamref name="TResult"/>.
    /// </summary>
    Task<TResult> GetStructuredCompletionAsync<TResult>(
        string systemPrompt,
        string userInput,
        string deploymentName,
        CancellationToken cancellationToken = default);
}
