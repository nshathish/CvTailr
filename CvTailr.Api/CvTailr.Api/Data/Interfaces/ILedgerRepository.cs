using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Data.Interfaces;

public interface ILedgerRepository
{
    Task<LedgerEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Partial hierarchical partition key (cvId only) — all entries across all jobs for this CV.</summary>
    Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default);

    /// <summary>Full hierarchical partition key — entries for one specific job's tailoring.</summary>
    Task<List<LedgerEntry>> GetByCvAndJobIdAsync(string cvId, string jobId, CancellationToken cancellationToken = default);

    Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}
