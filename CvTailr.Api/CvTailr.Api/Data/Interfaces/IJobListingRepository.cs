using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Data.Interfaces;

public interface IJobListingRepository
{
    Task<JobListing?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(JobListing listing, CancellationToken cancellationToken = default);
}
