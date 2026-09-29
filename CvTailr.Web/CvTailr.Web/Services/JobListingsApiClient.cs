using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class JobListingsApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IJobListingsApiClient
{
    public async Task<List<ListingMatch>> GetMatchesAsync(int limit, CancellationToken ct = default)
    {
        var relativePath = $"api/job-listings/matches?limit={limit}";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "GET", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<List<ListingMatch>>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }

    public async Task<string> AddToMyJobsAsync(string listingId, CancellationToken ct = default)
    {
        var relativePath = $"api/job-listings/{Uri.EscapeDataString(listingId)}/add-to-my-jobs";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        // 200 (already added) and 201 (newly created) share the same { jobId } body shape.
        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<AddToMyJobsResponse>(JsonOptions, ct);
        return result?.JobId ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body.");
    }

    private record AddToMyJobsResponse(string JobId);
}

// Mirrors CvTailr.Api's JobListingEndpoints/IJobListingsService JobListingMatch exactly.
public record ListingMatch(
    string ListingId,
    string RoleTitle,
    string CompanyName,
    string? CompanyDomain,
    string? Location,
    string? EmploymentType,
    string ApplyUrl,
    int MatchPercent,
    List<string> MatchedSkills,
    int RequirementCount,
    DateTimeOffset LastSeenAt);
