using CvTailr.Shared.Cv;
using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Services.Interfaces;

public interface ILedgerService
{
    /// <summary>
    /// Scans a job's TailoredCvDocument for provisional bullets/skills and registers a
    /// LedgerEntry (scoped to this cvId + jobId) for any that don't already have one.
    /// </summary>
    Task<List<LedgerEntry>> RegisterProvisionalItemsAsync(
        string cvId, string jobId, TailoredCvDocument tailoredDocument, CancellationToken cancellationToken = default);

    /// <summary>All entries across all jobs for this CV (partial hierarchical partition key).</summary>
    Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default);

    /// <summary>Entries for one specific job's tailoring (full hierarchical partition key).</summary>
    Task<List<LedgerEntry>> GetByCvAndJobIdAsync(string cvId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>Throws KeyNotFoundException if the entry doesn't exist.</summary>
    Task<LedgerEntry> RecordDrillAttemptAsync(string ledgerEntryId, DrillAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>Read-only — never mutates LedgerEntry.Status. Throws KeyNotFoundException if the entry doesn't exist.</summary>
    Task<LedgerReviewRecommendation> ReviewAsync(string ledgerEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ONLY method permitted to change LedgerEntry.Status. Must only be called as a result of
    /// an explicit, separate user-confirmation API call — never automatically. Throws
    /// KeyNotFoundException if the entry doesn't exist. When the new status is Confirmed or
    /// Removed, also updates the entry's job-scoped TailoredCvDocument (clearing IsProvisional, or
    /// stripping the bullet/skill entirely) — never the master CvDocument.
    /// </summary>
    Task<LedgerEntry> ConfirmStatusChangeAsync(string ledgerEntryId, LedgerStatus newStatus, CancellationToken cancellationToken = default);
}
