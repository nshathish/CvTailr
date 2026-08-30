namespace CvTailr.Shared.Ledger;

public class LedgerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubjectName { get; set; } = string.Empty;
    public string? RelatedBulletId { get; set; }
    public LedgerStatus Status { get; set; } = LedgerStatus.Provisional;
    public List<DrillAttempt> DrillHistory { get; set; } = new();
    public DateTimeOffset LastReviewed { get; set; } = DateTimeOffset.UtcNow;

    public bool HasRecentWeakPerformance(int loopBack = 3, int failThreshold = 2)
    {
        var recent = DrillHistory
            .OrderByDescending(a => a.Timestamp)
            .Take(loopBack)
            .Count(a => a.Outcome != DrillOutcome.Pass);

        return recent >= failThreshold;
    }
}