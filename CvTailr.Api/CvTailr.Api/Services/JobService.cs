using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Scoring;

namespace CvTailr.Api.Services;

public class JobService(IJobRepository jobRepository) : IJobService
{
    public async Task<Job> CreateFromJdAsync(string userId, JdRequirements jdRequirements, CancellationToken cancellationToken = default)
    {
        var job = new Job
        {
            UserId = userId,
            JdRequirements = jdRequirements,
            Status = JobStatus.Draft
        };

        await jobRepository.UpsertAsync(job, cancellationToken);
        return job;
    }

    public async Task<Job> AttachScoreAsync(string userId, string jobId, MatchScoreResult scoreResult, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetByIdAsync(userId, jobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job '{jobId}' was not found for this user.");

        job.MatchScoreResult = scoreResult;
        job.Status = JobStatus.Scored;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        await jobRepository.UpsertAsync(job, cancellationToken);
        return job;
    }

    public async Task<Job> MarkTailoredAsync(string userId, string jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetByIdAsync(userId, jobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job '{jobId}' was not found for this user.");

        job.Status = JobStatus.Tailored;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        await jobRepository.UpsertAsync(job, cancellationToken);
        return job;
    }

    public Task<List<Job>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        jobRepository.GetByUserIdAsync(userId, cancellationToken);

    public Task<Job?> GetByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default) =>
        jobRepository.GetByIdAsync(userId, jobId, cancellationToken);
}
