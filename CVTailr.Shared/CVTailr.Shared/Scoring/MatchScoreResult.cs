namespace CvTailr.Shared.Scoring;

public class MatchScoreResult
{
    public string CvId { get; set; } = string.Empty;
    public string JdId { get; set; } = string.Empty;
    public int OverallScorePercent { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public List<RequirementMatch> RequirementMatches { get; set; } = new();

    /// <summary>
    ///     When this score was computed. CvId can't detect a since-replaced CV (the master
    ///     CvDocument's Id is preserved across re-uploads), so staleness is instead detected by
    ///     comparing this against the master CvDocument's UpdatedAt.
    /// </summary>
    public DateTimeOffset ScoredAt { get; set; } = DateTimeOffset.UtcNow;

    public IEnumerable<string> MissingMustHaves =>
        RequirementMatches.Where(m => !m.IsMet).Select(m => m.Skill);
}
