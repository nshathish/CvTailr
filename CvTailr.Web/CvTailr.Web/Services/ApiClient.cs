using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
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

    public async Task<CvDocument?> GetCurrentCvAsync(CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = "api/cv/current";
            },
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"GET /api/cv/current failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CvDocument>(JsonOptions, ct);
        return result ?? throw new ApiClientException("GET /api/cv/current returned an empty response body.");
    }

    // The Api's /api/score returns 404 when the user hasn't uploaded a CV yet — an
    // expected "not ready" state, not a failure, so callers get a distinct signal
    // (ScoreOutcome.CvMissing) rather than an ApiClientException to branch on.
    public async Task<ScoreOutcome> GetScoreAsync(JdRequirements jdRequirements, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/score";
            },
            content: JsonContent.Create(new { jdRequirements }, options: JsonOptions),
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ScoreOutcome(Score: null, CvMissing: true);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/score failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<MatchScoreResult>(JsonOptions, ct);
        return new ScoreOutcome(
            Score: result ?? throw new ApiClientException("POST /api/score returned an empty response body."),
            CvMissing: false);
    }
}

public class ApiClientException(string message) : Exception(message);

public record ScoreOutcome(MatchScoreResult? Score, bool CvMissing);
