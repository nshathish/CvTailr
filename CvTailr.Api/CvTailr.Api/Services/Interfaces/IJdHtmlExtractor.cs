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
/// priority over whatever the LLM later infers from Text.
/// </summary>
public record JdHtmlExtractionResult(string Text, string? RoleTitle, string? CompanyName);
