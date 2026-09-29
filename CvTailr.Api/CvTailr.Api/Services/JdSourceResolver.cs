using CvTailr.Api.Exceptions;
using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

public class JdSourceResolver(
    IJdUrlFetcher jdUrlFetcher,
    IJdHtmlExtractor jdHtmlExtractor,
    ILogger<JdSourceResolver> logger) : IJdSourceResolver
{
    private const int MinReadableLength = 300;

    public async Task<JdSource> ResolveAsync(string? jdText, string? jdUrl, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(jdText))
        {
            return new JdSource(
                jdText, RoleTitle: null, CompanyName: null, SourceUrl: null,
                Location: null, EmploymentType: null, DatePosted: null, ValidThrough: null, PostingUrl: null,
                HiringOrganizationUrls: []);
        }

        var fetchResult = await jdUrlFetcher.FetchAsync(jdUrl!, cancellationToken);
        var extraction = jdHtmlExtractor.Extract(fetchResult.Html);

        if (extraction.Text.Length < MinReadableLength)
        {
            logger.LogWarning(
                "JD extraction for {Url} (final URL {FinalUrl}) produced unreadably short text ({Length} chars).",
                jdUrl, fetchResult.FinalUrl, extraction.Text.Length);

            throw new JdUrlException(
                "UrlNotReadable",
                "Couldn't read enough of that page — it may require login or rely on JavaScript. Try pasting the job description text instead.");
        }

        return new JdSource(
            extraction.Text, extraction.RoleTitle, extraction.CompanyName, fetchResult.FinalUrl,
            extraction.Location, extraction.EmploymentType, extraction.DatePosted, extraction.ValidThrough, extraction.PostingUrl,
            extraction.HiringOrganizationUrls);
    }
}
