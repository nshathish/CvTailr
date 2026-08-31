namespace CvTailr.Shared.Drill;

public class DrillQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Prompt { get; set; } = string.Empty;
    public DrillTargetType TargetType { get; set; }
    public string? RelatedLedgerEntryId { get; set; }
    public string? RelatedRequirementId { get; set; }
}
