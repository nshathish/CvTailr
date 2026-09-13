using System.Net;
using CvTailr.Shared.Jobs;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class ScoreApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IScoreApiClient
{
    // The Api's /api/score uses 409 for "upload a CV first" and 404 for an unknown jobId,
    // so callers can branch on those expected outcomes without conflating them.
    public async Task<ScoreOutcome> GetScoreAsync(string jobId, CancellationToken ct = default)
    {
        const string relativePath = "api/score";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { jobId }, options: JsonOptions),
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return new ScoreOutcome(Job: null, CvMissing: true, JobMissing: false);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ScoreOutcome(Job: null, CvMissing: false, JobMissing: true);
        }

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return new ScoreOutcome(
            Job: result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body."),
            CvMissing: false,
            JobMissing: false);
    }
}

public record ScoreOutcome(Job? Job, bool CvMissing, bool JobMissing);
