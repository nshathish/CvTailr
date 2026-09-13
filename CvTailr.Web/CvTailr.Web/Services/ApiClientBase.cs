using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Web.Configuration;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

// Shared plumbing for every typed Api client (one per CvTailr.Api endpoint group — see
// CvTailr.Web/CLAUDE.md). Uses Microsoft.Identity.Web's IDownstreamApi, which acquires and
// attaches the signed-in user's bearer token on every call — no custom DelegatingHandler needed.
public abstract class ApiClientBase(IDownstreamApi downstreamApi)
{
    protected const string ServiceName = ConfigurationSections.DownstreamApi;

    // Matches CvTailr.Api's wire format: camelCase property names, enums as strings
    // (Api's Program.cs registers JsonStringEnumConverter on its HTTP JSON options).
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected IDownstreamApi DownstreamApi { get; } = downstreamApi;

    /// <summary>
    /// Throws ApiClientException with a consistent message if the response isn't a success status.
    /// Callers that need special handling for a specific status (e.g. 404 -> null) must check that
    /// before calling this, since it treats every non-success status as a failure.
    /// </summary>
    protected static async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response, string method, string relativePath, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ApiClientException(
            $"{method} /{relativePath} failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
    }
}
