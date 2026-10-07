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
    public async Task<JdParseOutcome> ParseJdAsync(string? jdText, string? jdUrl, CancellationToken ct = default)
    {
        const string relativePath = "api/jobs/parse";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { jdText, jdUrl }, options: JsonOptions),
            cancellationToken: ct);

        // The Api returns 422 with a specific error code for a URL it couldn't turn into JD text
        // (see CvTailr.Api's JobEndpoints/JdServiceClient, which proxies this from CvTailr.Jd.Api)
        // — map the ones the page knows how to show inline; anything else falls through to the
        // generic error handling below.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<JdParseError>(JsonOptions, ct);
            var urlError = error?.Code switch
            {
                "UrlNotAllowed" => JdUrlErrorKind.NotAllowed,
                "UrlFetchFailed" => JdUrlErrorKind.FetchFailed,
                "UrlNotReadable" => JdUrlErrorKind.NotReadable,
                _ => (JdUrlErrorKind?)null
            };

            if (urlError is not null)
                return new JdParseOutcome(Job: null, UrlError: urlError);

            throw new ApiClientException(
                $"POST /{relativePath} failed with status 422 {response.StatusCode}: {error?.Message ?? "Unprocessable request."}");
        }

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return new JdParseOutcome(
            Job: result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body."),
            UrlError: null);
    }

    private record JdParseError(string Code, string Message);

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

    public async Task<Job> UpdateJobDetailsAsync(
        string jobId, string roleTitle, string companyName, string? companyDomain, CancellationToken ct = default)
    {
        var relativePath = $"api/jobs/{Uri.EscapeDataString(jobId)}/details";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "PUT";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { roleTitle, companyName, companyDomain }, options: JsonOptions),
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "PUT", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"PUT /{relativePath} returned an empty response body.");
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
public enum JdUrlErrorKind
{
    NotAllowed,
    FetchFailed,
    NotReadable
}

public record JdParseOutcome(Job? Job, JdUrlErrorKind? UrlError);

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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum JobStatus
{
    Scored,
    Preparing,
    Tailored
}

public record PrepSummary(int Total, int Ready, int AssessedPassed);
