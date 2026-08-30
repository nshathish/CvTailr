namespace CvTailr.Shared.Scoring;

public class RequirementMatch
{
    public string RequirementId { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public int Confidence { get; set; }
    public string? SupportingEvidence { get; set; }
    public string? GapNote { get; set; }
}
