using CvTailr.Shared.Cv;

namespace CvTailr.Api.Data;

public interface ICvRepository
{
    Task<CvDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>One CvDocument per UserId — always replaces whatever is currently stored for that user.</summary>
    Task UpsertAsync(CvDocument document, CancellationToken cancellationToken = default);
}
