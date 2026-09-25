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

    /// <summary>Lists the user's Jobs as response DTOs.</summary>
    Task<List<JobResponse>> GetResponsesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Single-Job counterpart to GetResponsesForUserAsync.</summary>
    Task<JobResponse?> GetResponseByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A Job as returned to API clients: the stored Job's fields plus an optional prep-readiness
/// summary. PrepSummary is always null for now — populating it is a separate future task.
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
