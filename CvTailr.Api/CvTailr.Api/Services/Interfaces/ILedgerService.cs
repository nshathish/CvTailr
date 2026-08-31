using CvTailr.Shared.Cv;
using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Services.Interfaces;

public interface ILedgerService
{
    Task<List<LedgerEntry>> RegisterProvisionalItemsAsync(CvDocument cvDocument, CancellationToken cancellationToken = default);

    Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default);

    /// <summary>Throws KeyNotFoundException if the entry doesn't exist.</summary>
    Task<LedgerEntry> RecordDrillAttemptAsync(string ledgerEntryId, DrillAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>Read-only — never mutates LedgerEntry.Status. Throws KeyNotFoundException if the entry doesn't exist.</summary>
    Task<LedgerReviewRecommendation> ReviewAsync(string ledgerEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ONLY method permitted to change LedgerEntry.Status. Must only be called as a result of
    /// an explicit, separate user-confirmation API call — never automatically. Throws
    /// KeyNotFoundException if the entry doesn't exist.
    /// </summary>
    Task<LedgerEntry> ConfirmStatusChangeAsync(string ledgerEntryId, LedgerStatus newStatus, CancellationToken cancellationToken = default);
}
