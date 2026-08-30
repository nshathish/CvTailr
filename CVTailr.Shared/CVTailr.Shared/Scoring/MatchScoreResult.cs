namespace CvTailr.Shared.Scoring;

public class MatchScoreResult
{
    public string CvId { get; set; } = string.Empty;
    public string JdId { get; set; } = string.Empty;
    public int OverallScorePercent { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public List<RequirementMatch> RequirementMatches { get; set; } = new();

    public IEnumerable<string> MissingMustHaves =>
        RequirementMatches.Where(m => !m.IsMet).Select(m => m.Skill);
}
