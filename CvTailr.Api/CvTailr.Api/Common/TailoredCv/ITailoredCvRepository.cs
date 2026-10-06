using CvTailr.Shared.Cv;

namespace CvTailr.Api.Common.TailoredCv;

public interface ITailoredCvRepository
{
    Task<TailoredCvDocument?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>One TailoredCvDocument per JobId — always replaces whatever is currently stored for that job.</summary>
    Task UpsertAsync(TailoredCvDocument document, CancellationToken cancellationToken = default);

    Task DeleteByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
