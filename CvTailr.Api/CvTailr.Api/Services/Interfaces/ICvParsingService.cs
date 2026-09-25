using CvTailr.Shared.Cv;

namespace CvTailr.Api.Services.Interfaces;

public interface ICvParsingService
{
    /// <summary>Parses raw CV source text into a CvDocument, persists it as the given user's current CV, and returns it.</summary>
    Task<CvDocument> UploadAndParseAsync(string userId, string rawCvSource, string? sourceFileName,
        CancellationToken cancellationToken = default);

    Task<CvDocument?> GetCurrentAsync(string userId, CancellationToken cancellationToken = default);
}
