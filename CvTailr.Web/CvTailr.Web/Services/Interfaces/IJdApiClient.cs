using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface IJdApiClient
{
    /// <summary>Exactly one of jdText/jdUrl must be non-empty.</summary>
    Task<JdParseOutcome> ParseJdAsync(string? jdText, string? jdUrl, CancellationToken ct = default);
}
