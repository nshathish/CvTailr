using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface IJobListingsApiClient
{
    Task<List<ListingMatch>> GetMatchesAsync(int limit, CancellationToken ct = default);

    /// <summary>Returns the JobId from either the Api's 200 (already added) or 201 (newly created) response.</summary>
    Task<string> AddToMyJobsAsync(string listingId, CancellationToken ct = default);
}
