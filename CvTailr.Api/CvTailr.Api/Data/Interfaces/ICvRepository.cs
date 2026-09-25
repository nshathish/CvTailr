using CvTailr.Shared.Cv;

namespace CvTailr.Api.Data.Interfaces;

public interface ICvRepository
{
    Task<CvDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(CvDocument document, CancellationToken cancellationToken = default);
}
