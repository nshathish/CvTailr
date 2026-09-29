using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Helpers.Extensions;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Services;

public class JobListingsService(
    ICvRepository cvRepository,
    IJobRepository jobRepository,
    IJobListingRepository jobListingRepository,
    ITailoredCvRepository tailoredCvRepository,
    ISkillTaggingService skillTaggingService,
    IScoringService scoringService,
    IJobService jobService,
    ILogger<JobListingsService> logger) : IJobListingsService
{
    private const int MinMatchPercent = 40;
    private const int MaxMatchedSkills = 5;
    private const int MaxLastSeenAgeDays = 60;

    public async Task<List<JobListingMatch>> GetMatchesAsync(
        string userId, int limit, CancellationToken cancellationToken = default)
    {
        var cv = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cv is null)
            return [];

        var tags = await GetOrComputeSkillTagsAsync(cv, cancellationToken);
        if (tags.Count == 0)
            return [];

        var candidates = await jobListingRepository.QueryActiveMatchesAsync(tags, cancellationToken);
        if (candidates.Count == 0)
            return [];

        var userJobs = await jobRepository.GetByUserIdAsync(userId, cancellationToken);
        var excludedListingIds = userJobs
            .Where(j => j.JobListingId is not null)
            .Select(j => j.JobListingId!)
            .ToHashSet();

        var matches = new List<JobListingMatch>();
        foreach (var listing in candidates)
        {
            if (excludedListingIds.Contains(listing.Id))
                continue;

            var score = JobListingMatcher.Calculate(listing.Requirements, tags);
            if (score.MatchPercent < MinMatchPercent)
                continue;

            matches.Add(new JobListingMatch(
                listing.Id,
                listing.RoleTitle,
                listing.CompanyName,
                listing.CompanyDomain,
                listing.Location,
                listing.EmploymentType,
                listing.ApplyUrl,
                score.MatchPercent,
                score.MatchedSkills.Take(MaxMatchedSkills).ToList(),
                listing.Requirements.Count,
                listing.LastSeenAt));
        }

        return matches
            .OrderByDescending(m => m.MatchPercent)
            .ThenByDescending(m => m.LastSeenAt)
            .Take(limit)
            .ToList();
    }

    public async Task<AddToMyJobsOutcome?> AddToMyJobsAsync(
        string userId, string listingId, CancellationToken cancellationToken = default)
    {
        var listing = await jobListingRepository.GetAsync(listingId, cancellationToken);
        if (listing is null || !PassesActivityRules(listing))
            return null;

        var userJobs = await jobRepository.GetByUserIdAsync(userId, cancellationToken);
        var existingJob = userJobs.FirstOrDefault(j => j.JobListingId == listingId);
        if (existingJob is not null)
            return new AddToMyJobsOutcome(existingJob.Id, AlreadyExists: true);

        var jdRequirements = new JdRequirements
        {
            RoleTitle = listing.RoleTitle,
            CompanyName = listing.CompanyName,
            CompanyDomain = listing.CompanyDomain,
            RawJdText = string.Empty,
            Requirements = listing.Requirements.Select(r => new JdRequirement
            {
                Skill = r.Skill,
                Priority = r.Priority,
                YearsRequired = r.YearsRequired,
                Notes = r.Notes
            }).ToList(),
            EmphasizedLanguages = []
        };

        var sourceMetadata = new JobSourceMetadata
        {
            Location = listing.Location,
            EmploymentType = listing.EmploymentType,
            DatePosted = listing.DatePosted,
            ValidThrough = listing.ValidThrough
        };

        var job = await jobService.CreateFromJdAsync(
            userId, jdRequirements, listing.ApplyUrl, sourceMetadata, cancellationToken);

        job.JobListingId = listing.Id;
        await jobRepository.UpsertAsync(job, cancellationToken);

        // Same services /api/score uses: this job's TailoredCvDocument if one already exists
        // (it won't, for a job this new), otherwise the user's master CV. Scoring is skipped
        // (not failed) when there's no CV to score against.
        var cvDocument = await tailoredCvRepository.ResolveBaseDocumentAsync(cvRepository, userId, job.Id, cancellationToken);
        if (cvDocument is not null)
        {
            var scoreResult = await scoringService.ScoreAsync(job.JdRequirements, cvDocument, cancellationToken);
            await jobService.AttachScoreAsync(userId, job.Id, scoreResult, cancellationToken);
        }

        return new AddToMyJobsOutcome(job.Id, AlreadyExists: false);
    }

    private static bool PassesActivityRules(JobListing listing) =>
        listing.Status == JobListingStatus.Active &&
        (listing.ValidThrough is null || listing.ValidThrough >= DateTimeOffset.UtcNow) &&
        listing.LastSeenAt >= DateTimeOffset.UtcNow.AddDays(-MaxLastSeenAgeDays);

    private async Task<List<string>> GetOrComputeSkillTagsAsync(CvDocument cv, CancellationToken cancellationToken)
    {
        if (cv.SkillTags is not null)
            return cv.SkillTags;

        try
        {
            var tags = await skillTaggingService.TagCvAsync(cv, cancellationToken);
            cv.SkillTags = tags;
            cv.SkillTagsUpdatedAt = DateTimeOffset.UtcNow;
            await cvRepository.UpsertAsync(cv, cancellationToken);
            return tags;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skill tagging failed while computing job-listing matches; proceeding with no tags.");
            return [];
        }
    }
}
