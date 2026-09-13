using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Data.Interfaces;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>Ordered by UpdatedAt descending (most recent first) for the Dashboard list.</summary>
    Task<List<Job>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(Job job, CancellationToken cancellationToken = default);
}
