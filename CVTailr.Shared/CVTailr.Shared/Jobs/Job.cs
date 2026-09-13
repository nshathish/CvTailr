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
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
