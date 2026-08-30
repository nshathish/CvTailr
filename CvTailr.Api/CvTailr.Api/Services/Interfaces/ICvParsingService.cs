using CvTailr.Shared.Cv;

namespace CvTailr.Api.Services.Interfaces;

public interface ICvParsingService
{
    Task<CvDocument> ParseAsync(string rawLatexSource, CancellationToken cancellationToken = default);
}
