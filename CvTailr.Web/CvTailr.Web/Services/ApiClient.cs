using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Tailoring;
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

    public async Task<Job> ParseJdAsync(string jdText, CancellationToken ct = default)
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

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
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

    public async Task<Job?> GetJobByIdAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = $"api/jobs/{Uri.EscapeDataString(jobId)}";
            },
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"GET /api/jobs/{jobId} failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /api/jobs/{jobId} returned an empty response body.");
    }

    public async Task<List<Job>> GetJobsAsync(CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = "api/jobs";
            },
            cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"GET /api/jobs failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<List<Job>>(JsonOptions, ct);
        return result ?? throw new ApiClientException("GET /api/jobs returned an empty response body.");
    }

    // The Api's /api/score returns 404 both when the user hasn't uploaded a CV yet and
    // when the given jobId doesn't resolve to a persisted Job — either way it's an
    // expected "not ready" state, not a failure, so callers get a distinct signal
    // (ScoreOutcome.CvMissing) rather than an ApiClientException to branch on.
    public async Task<ScoreOutcome> GetScoreAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/score";
            },
            content: JsonContent.Create(new { jobId }, options: JsonOptions),
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ScoreOutcome(Job: null, CvMissing: true);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/score failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return new ScoreOutcome(
            Job: result ?? throw new ApiClientException("POST /api/score returned an empty response body."),
            CvMissing: false);
    }
    public async Task<TailoringProposal> ProposeTailoringAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/tailor/propose";
            },
            content: JsonContent.Create(new { jobId }, options: JsonOptions),
            cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/tailor/propose failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<TailoringProposal>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/tailor/propose returned an empty response body.");
    }

    // Mirrors the Api's TailorApplyRequest shape exactly: ApprovedBulletRewriteIds says which
    // rewrites to apply, while ProposedBulletRewrites carries the full proposal objects the Api
    // needs to look up by that id — sending only the approved subset for both is sufficient (the
    // Api intersects by id) and keeps the request smaller. New bullets/skills carry no separate
    // id list since the client only ever sends the ones it's approving.
    public async Task<TailorApplyResult> ApplyTailoringAsync(
        string jobId,
        List<string> approvedBulletRewriteIds,
        List<BulletRewriteProposal> approvedRewrites,
        List<NewBulletProposal> approvedNewBullets,
        List<NewSkillProposal> approvedNewSkills,
        CancellationToken ct = default)
    {
        using var response = await downstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = "api/tailor/apply";
            },
            content: JsonContent.Create(
                new
                {
                    jobId,
                    approvedBulletRewriteIds,
                    proposedBulletRewrites = approvedRewrites,
                    approvedNewBullets,
                    approvedNewSkills
                },
                options: JsonOptions),
            cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/tailor/apply failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<TailorApplyResult>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/tailor/apply returned an empty response body.");
    }
}

public class ApiClientException(string message) : Exception(message);

public record ScoreOutcome(Job? Job, bool CvMissing);

public record TailorApplyResult(CvDocument CvDocument, Job Job, List<string> Warnings);
