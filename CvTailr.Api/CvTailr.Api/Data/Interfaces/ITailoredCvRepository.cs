using CvTailr.Shared.Cv;

namespace CvTailr.Api.Data.Interfaces;

public interface ITailoredCvRepository
{
    Task<TailoredCvDocument?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// JobIds (within this user's Jobs) that currently have a TailoredCvDocument — one query, for
    /// joining against a Job list in memory rather than looking up each job's tailored doc individually.
    /// </summary>
    Task<List<string>> GetJobIdsByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>One TailoredCvDocument per JobId — always replaces whatever is currently stored for that job.</summary>
    Task UpsertAsync(TailoredCvDocument document, CancellationToken cancellationToken = default);

    Task DeleteByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
