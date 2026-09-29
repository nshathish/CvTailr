namespace CvTailr.Api.Services.Interfaces;

/// <summary>
/// Extracts job-posting text (and, when available, title/company) from the raw HTML fetched by
/// <see cref="IJdUrlFetcher"/>. Prefers an embedded JobPosting JSON-LD block; falls back to the
/// page's visible text otherwise.
/// </summary>
public interface IJdHtmlExtractor
{
    JdHtmlExtractionResult Extract(string html);
}

/// <summary>
/// RoleTitle/CompanyName are populated only when found in structured (JSON-LD) data — they take
/// priority over whatever the LLM later infers from Text. Location/EmploymentType/DatePosted/
/// ValidThrough/PostingUrl are likewise JSON-LD-only (null on the plain-text fallback path) and
/// exist solely for CvTailr.Api's JobListing capture (task 022) — they are never sent to the Web
/// client. HiringOrganizationUrls (hiringOrganization.url, then each sameAs entry, in order; empty
/// when absent) feeds CompanyDomain resolution (task 024) and is likewise server-side only.
/// </summary>
public record JdHtmlExtractionResult(
    string Text,
    string? RoleTitle,
    string? CompanyName,
    string? Location,
    string? EmploymentType,
    DateTimeOffset? DatePosted,
    DateTimeOffset? ValidThrough,
    string? PostingUrl,
    List<string> HiringOrganizationUrls);
