using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Scoring;

namespace CvTailr.Api.Services;

public class JobService(IJobRepository jobRepository, ITailoredCvRepository tailoredCvRepository) : IJobService
{
    public async Task<Job> CreateFromJdAsync(string userId, JdRequirements jdRequirements, CancellationToken cancellationToken = default)
    {
        var job = new Job
        {
            UserId = userId,
            JdRequirements = jdRequirements,
            // JobStatus has no "unscored" member — Scored is the earliest of the three lifecycle
            // stages, so a freshly parsed job starts there until /api/score attaches an actual score.
            Status = JobStatus.Scored
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

    public async Task<bool> DeleteAsync(string userId, string jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetByIdAsync(userId, jobId, cancellationToken);
        if (job is null)
            return false;

        await jobRepository.DeleteAsync(userId, jobId, cancellationToken);
        return true;
    }

    public async Task<List<JobResponse>> GetResponsesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var jobs = await jobRepository.GetByUserIdAsync(userId, cancellationToken);
        var tailoredJobIds = await tailoredCvRepository.GetJobIdsByUserIdAsync(userId, cancellationToken);
        var tailoredJobIdSet = tailoredJobIds.ToHashSet();

        return jobs.Select(job => ToResponse(job, tailoredJobIdSet.Contains(job.Id))).ToList();
    }

    public async Task<JobResponse?> GetResponseByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetByIdAsync(userId, jobId, cancellationToken);
        if (job is null)
            return null;

        var tailoredCv = await tailoredCvRepository.GetByJobIdAsync(jobId, cancellationToken);
        return ToResponse(job, tailoredCv is not null);
    }

    // Legacy jobs never got their stored Status flipped to Tailored if they were tailored before
    // that write existed — TailoredCvDocument existence, not the stored Status, is the source of
    // truth for "has this job actually been tailored," so correct it here on read rather than
    // mutating storage.
    private static JobResponse ToResponse(Job job, bool hasTailoredCv)
    {
        var status = job.Status == JobStatus.Scored && hasTailoredCv ? JobStatus.Tailored : job.Status;

        return new JobResponse(
            job.Id,
            job.UserId,
            job.JdRequirements,
            job.MatchScoreResult,
            status,
            PrepSummary: null,
            job.CreatedAt,
            job.UpdatedAt);
    }
}
