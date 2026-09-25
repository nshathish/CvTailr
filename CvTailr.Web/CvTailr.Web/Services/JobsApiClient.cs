using System.Net;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Scoring;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class JobsApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IJobsApiClient
{
    public async Task<Job?> GetJobByIdAsync(string jobId, CancellationToken ct = default)
    {
        var relativePath = $"api/jobs/{Uri.EscapeDataString(jobId)}";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfUnsuccessfulAsync(response, "GET", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }

    public async Task<JobCvResponse?> GetJobCvAsync(string jobId, CancellationToken ct = default)
    {
        var relativePath = $"api/jobs/{Uri.EscapeDataString(jobId)}/cv";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfUnsuccessfulAsync(response, "GET", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<JobCvResponse>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }

    public async Task<List<JobSummary>> GetJobsAsync(CancellationToken ct = default)
    {
        const string relativePath = "api/jobs";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "GET", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<List<JobSummary>>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken ct = default)
    {
        var relativePath = $"api/jobs/{Uri.EscapeDataString(jobId)}";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "DELETE";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "DELETE", relativePath, ct);
    }
}

// Mirrors CvTailr.Api's JobEndpoints.JobCvResponse exactly (Document, IsTailored). Reuses
// CvTailr.Shared.Cv.TailoredCvDocument directly rather than a duplicate local copy — see
// CvTailr.Web/CLAUDE.md's "How this project talks to the Api" section.
public record JobCvResponse(TailoredCvDocument Document, bool IsTailored);

// Mirrors CvTailr.Api's JobEndpoints.JobResponse — the dashboard-list shape, trimmed to the
// fields the job cards actually render. JdRequirements/MatchScoreResult keep reusing the Shared
// types (unchanged by this DTO), but Status and PrepSummary are declared fresh here rather than
// reusing CvTailr.Shared.Jobs' versions of them.
public record JobSummary(
    string Id,
    JdRequirements JdRequirements,
    MatchScoreResult? MatchScoreResult,
    JobStatus Status,
    PrepSummary? PrepSummary,
    DateTimeOffset UpdatedAt);

public enum JobStatus
{
    Scored,
    Preparing,
    Tailored
}

public record PrepSummary(int Total, int Ready, int AssessedPassed);
