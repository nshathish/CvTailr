namespace CvTailr.Api.Services.Interfaces;

/// <summary>
/// Resolves the value submitted in a JD-parse request into raw JD text — passing raw text
/// through unchanged, or fetching the content of a job posting URL.
/// </summary>
public interface IJdSourceResolver
{
    Task<string> ResolveAsync(string input, CancellationToken cancellationToken = default);
}
