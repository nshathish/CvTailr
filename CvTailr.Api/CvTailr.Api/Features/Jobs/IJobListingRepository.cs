using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Features.Jobs;

public interface IJobListingRepository
{
    Task<JobListing?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(JobListing listing, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active listings sharing at least one of the given tags, excluding those whose ValidThrough
    /// is in the past or whose LastSeenAt is older than 60 days. Capped at 500 items; tags must be
    /// non-empty.
    /// </summary>
    Task<List<JobListing>> QueryActiveMatchesAsync(List<string> tags, CancellationToken cancellationToken = default);
}
