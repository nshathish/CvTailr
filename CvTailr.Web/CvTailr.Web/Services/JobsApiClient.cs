using System.Net;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;
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

    public async Task<List<Job>> GetJobsAsync(CancellationToken ct = default)
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

        var result = await response.Content.ReadFromJsonAsync<List<Job>>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }
}

// Mirrors CvTailr.Api's JobEndpoints.JobCvResponse exactly (Document, IsTailored). Reuses
// CvTailr.Shared.Cv.TailoredCvDocument directly rather than a duplicate local copy — see
// CvTailr.Web/CLAUDE.md's "How this project talks to the Api" section.
public record JobCvResponse(TailoredCvDocument Document, bool IsTailored);
