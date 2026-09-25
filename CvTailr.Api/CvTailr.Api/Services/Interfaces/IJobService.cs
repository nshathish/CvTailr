using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Scoring;

namespace CvTailr.Api.Services.Interfaces;

public interface IJobService
{
    Task<Job> CreateFromJdAsync(string userId, JdRequirements jdRequirements, CancellationToken cancellationToken = default);

    /// <summary>Throws KeyNotFoundException if no Job with this id exists for this user.</summary>
    Task<Job> AttachScoreAsync(string userId, string jobId, MatchScoreResult scoreResult, CancellationToken cancellationToken = default);

    /// <summary>Throws KeyNotFoundException if no Job with this id exists for this user.</summary>
    Task<Job> MarkTailoredAsync(string userId, string jobId, CancellationToken cancellationToken = default);

    Task<List<Job>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<Job?> GetByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>Deletes only the Job record itself. Returns false if no Job with this id exists for this user.</summary>
    Task<bool> DeleteAsync(string userId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the user's Jobs as response DTOs. Corrects any Job whose stored Status is Scored but
    /// which already has a TailoredCvDocument (jobs tailored before MarkTailoredAsync's write
    /// existed) to report Tailored, checking every job's tailored-doc existence via one query
    /// against the user's TailoredCvDocuments rather than a lookup per job.
    /// </summary>
    Task<List<JobResponse>> GetResponsesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Single-Job counterpart to GetResponsesForUserAsync, with the same Status correction.</summary>
    Task<JobResponse?> GetResponseByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A Job as returned to API clients: the stored Job's fields, with Status corrected in memory where
/// needed (see GetResponsesForUserAsync), plus an optional prep-readiness summary — always null
/// until task 011 populates it.
/// </summary>
public record JobResponse(
    string Id,
    string UserId,
    JdRequirements JdRequirements,
    MatchScoreResult? MatchScoreResult,
    JobStatus Status,
    PrepSummary? PrepSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
