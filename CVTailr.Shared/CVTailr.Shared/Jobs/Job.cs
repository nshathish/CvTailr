using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;

namespace CvTailr.Shared.Jobs;

public class Job
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Partition key for persistence — one Job belongs to exactly one UserId.</summary>
    public string UserId { get; set; } = string.Empty;

    public JdRequirements JdRequirements { get; set; } = new();
    public MatchScoreResult? MatchScoreResult { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Scored;

    /// <summary>The job posting URL this Job was parsed from, or null if it came from pasted text.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>JSON-LD extras from SourceUrl, present only when the source page had any of them.</summary>
    public JobSourceMetadata? SourceMetadata { get; set; }

    /// <summary>The shared JobListing this Job was captured into or added from, if any.</summary>
    public string? JobListingId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
