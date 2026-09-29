namespace CvTailr.Api.Services.Interfaces;

/// <summary>
/// Resolves a JD-parse request's pasted text or job posting URL into JD text ready for
/// <see cref="IJdParsingService"/> — for a URL, fetches and extracts it via
/// <see cref="IJdUrlFetcher"/> and <see cref="IJdHtmlExtractor"/>. Throws
/// <see cref="CvTailr.Api.Exceptions.JdUrlException"/> on failure.
/// </summary>
public interface IJdSourceResolver
{
    Task<JdSource> ResolveAsync(string? jdText, string? jdUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// RoleTitle/CompanyName are populated only when the URL path found them in structured data —
/// they take priority over the LLM's own extraction. SourceUrl is the final URL after redirects,
/// present only when the request came from a URL. Location/EmploymentType/DatePosted/
/// ValidThrough/PostingUrl are JSON-LD-only extras carried through purely for JobListing capture
/// (task 022) — server-side only, never part of the Api's response to the Web client.
/// </summary>
public record JdSource(
    string Text,
    string? RoleTitle,
    string? CompanyName,
    string? SourceUrl,
    string? Location,
    string? EmploymentType,
    DateTimeOffset? DatePosted,
    DateTimeOffset? ValidThrough,
    string? PostingUrl);
