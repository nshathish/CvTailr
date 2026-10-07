using CvTailr.Jd.Api.Domain;
using CvTailr.Jd.Api.Domain.CompanyDomain;
using CvTailr.Jd.Api.Infrastructure;
using CvTailr.Shared.Jobs;

namespace CvTailr.Jd.Api.Application;

public sealed class JdParseOrchestrator(
    IJdSourceResolver jdSourceResolver,
    IJdParsingService jdParsingService) : IJdParseOrchestrator
{
    private static bool HasAnySourceMetadata(JdSource source) =>
        source.Location is not null || source.EmploymentType is not null ||
        source.DatePosted is not null || source.ValidThrough is not null || source.PostingUrl is not null;

    public async Task<JdParseResult> ParseAsync(string? jdText, string? jdUrl, CancellationToken cancellationToken)
    {
        var source = await jdSourceResolver.ResolveAsync(jdText, jdUrl, cancellationToken);

        var jdRequirements = await jdParsingService.ParseAsync(source.Text, cancellationToken);

        // Structured (JSON-LD) title/company from the source page take priority over the LLM's.
        if (!string.IsNullOrWhiteSpace(source.RoleTitle))
            jdRequirements.RoleTitle = source.RoleTitle;
        if (!string.IsNullOrWhiteSpace(source.CompanyName))
            jdRequirements.CompanyName = source.CompanyName;

        // First valid candidate wins: hiringOrganization URLs, then the source page's own
        // domain, then the LLM's guess — each normalised and checked against the exclusion
        // list (job boards/ATS hosts, generic email/social domains).
        jdRequirements.CompanyDomain = CompanyDomainResolver.Resolve(
            source.HiringOrganizationUrls, source.SourceUrl, jdRequirements.CompanyDomain);

        var sourceMetadata = HasAnySourceMetadata(source)
            ? new JobSourceMetadata
            {
                Location = source.Location,
                EmploymentType = source.EmploymentType,
                DatePosted = source.DatePosted,
                ValidThrough = source.ValidThrough,
                PostingUrl = source.PostingUrl
            }
            : null;

        return new JdParseResult(jdRequirements, source.SourceUrl, sourceMetadata);
    }
}