using CvTailr.Shared.Cv;

namespace CvTailr.Web.Services.Interfaces;

public interface ICvApiClient
{
    Task<CvDocument> ParseCvAsync(string rawLatexSource, CancellationToken ct = default);

    Task<CvDocument?> GetCurrentCvAsync(CancellationToken ct = default);
}
