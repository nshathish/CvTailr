using CvTailr.Shared.Cv;

namespace CvTailr.Api.Features.Cv;

public interface ICvRepository
{
    Task<CvDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(CvDocument document, CancellationToken cancellationToken = default);
}
