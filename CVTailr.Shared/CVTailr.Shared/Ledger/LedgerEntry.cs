namespace CvTailr.Shared.Ledger;

public class LedgerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Together with <see cref="JobId"/>, forms the Cosmos hierarchical partition key.</summary>
    public string CvId { get; set; } = string.Empty;

    /// <summary>
    /// The job this provisional item was tailored for. Provisional/confirmed/removed status is
    /// tracked per (CvId, JobId) — the same claim can be provisional for one job's tailoring and
    /// not exist at all for another.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

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