using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Web.Configuration;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

// The one place HTTP calls to CvTailr.Api happen (see CvTailr.Web/CLAUDE.md).
// Uses Microsoft.Identity.Web's IDownstreamApi (see the DownstreamApi section in
// appsettings.json and Program.cs's AddDownstreamApi call), which acquires and attaches
// the signed-in user's bearer token on every call — no custom DelegatingHandler needed.
public class ApiClient(IDownstreamApi downstreamApi)
{
    private const string ServiceName = ConfigurationSections.DownstreamApi;

    // Matches CvTailr.Api's wire format: camelCase property names, enums as strings
    // (Api's Program.cs registers JsonStringEnumConverter on its HTTP JSON options).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<JdRequirements> ParseJdAsync(string jdText, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/jd/parse";
            },
            content: JsonContent.Create(new { jdText }, options: JsonOptions),
            cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/jd/parse failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<JdRequirements>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/jd/parse returned an empty response body.");
    }

    public async Task<CvDocument> ParseCvAsync(string rawLatexSource, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/cv/parse";
            },
            content: JsonContent.Create(new { rawLatexSource }, options: JsonOptions),
            cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/cv/parse failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CvDocument>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/cv/parse returned an empty response body.");
    }
}

public class ApiClientException(string message) : Exception(message);
