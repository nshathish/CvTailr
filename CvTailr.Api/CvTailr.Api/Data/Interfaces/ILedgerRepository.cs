using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Data.Interfaces;

public interface ILedgerRepository
{
    Task<LedgerEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default);
    Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}
