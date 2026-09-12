using System.Net;
using CvTailr.Shared.Jobs;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class ScoreApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IScoreApiClient
{
    // The Api's /api/score returns 404 both when the user hasn't uploaded a CV yet and
    // when the given jobId doesn't resolve to a persisted Job — either way it's an
    // expected "not ready" state, not a failure, so callers get a distinct signal
    // (ScoreOutcome.CvMissing) rather than an ApiClientException to branch on.
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

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ScoreOutcome(Job: null, CvMissing: true);
        }

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return new ScoreOutcome(
            Job: result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body."),
            CvMissing: false);
    }
}

public record ScoreOutcome(Job? Job, bool CvMissing);
