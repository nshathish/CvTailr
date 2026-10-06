namespace CvTailr.Api.Features.Cv;

/// <summary>
/// Extracts plain text from an uploaded CV file, regardless of source format. Throws
/// <see cref="Cv.UnsupportedCvFormatException"/> for an unrecognized
/// extension or when no readable text could be extracted.
/// </summary>
public interface ICvSourceExtractor
{
    Task<string> ExtractAsync(Stream fileStream, string fileName, CancellationToken ct);
}
