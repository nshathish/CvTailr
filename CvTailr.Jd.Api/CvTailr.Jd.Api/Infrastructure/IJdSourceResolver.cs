using CvTailr.Jd.Api.Domain;
using CvTailr.Jd.Api.Infrastructure;

namespace CvTailr.Jd.Api.Infrastructure;

/// <summary>
/// Resolves a JD-parse request's pasted text or job posting URL into JD text ready for
/// <see cref="IJdParsingService"/> — for a URL, fetches and extracts it via
/// <see cref="JdUrlFetcher"/> and <see cref="JdHtmlExtractor"/>. Throws
/// <see cref="JdUrlException"/> on failure.
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
/// HiringOrganizationUrls likewise feeds CompanyDomain resolution (task 024) only.
/// </summary>
