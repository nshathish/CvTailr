using CvTailr.Shared.Cv;

namespace CvTailr.Web.Services.Interfaces;

public interface ICvApiClient
{
    Task<CvDocument> ParseCvAsync(Stream? fileStream, string? fileName, string? rawCvText, CancellationToken ct = default);

    Task<CvDocument?> GetCurrentCvAsync(CancellationToken ct = default);
}
