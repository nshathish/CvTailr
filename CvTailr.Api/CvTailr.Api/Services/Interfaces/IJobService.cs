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
}
