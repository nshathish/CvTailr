namespace CvTailr.Jd.Api.Domain.CompanyDomain;

/// <summary>
/// Resolves JdRequirements.CompanyDomain from parse-time candidates. Never infers a domain from
/// the company name — only from explicit URLs (JSON-LD, the source page itself) or the LLM's own
/// finding of an explicit company URL/email in the JD text.
/// </summary>
public static class CompanyDomainResolver
{
    /// <summary>Null when the candidate is missing, invalid, an IP/localhost, or excluded.</summary>
    public static string? Normalise(string? candidate)
    {
        var domain = JobListingUrlHelper.GetRegistrableDomain(candidate);
        return ExcludedCompanyDomains.IsExcluded(domain) ? null : domain;
    }

    /// <summary>
    /// First valid candidate wins: each hiringOrganizationUrls entry in order, then the
    /// registrable domain of the final page URL, then the LLM's own CompanyDomain guess.
    /// </summary>
    public static string? Resolve(IEnumerable<string> hiringOrganizationUrls, string? finalUrl, string? llmCompanyDomain)
    {
        foreach (var url in hiringOrganizationUrls)
        {
            var domain = Normalise(url);
            if (domain is not null)
                return domain;
        }

        return Normalise(finalUrl) ?? Normalise(llmCompanyDomain);
    }
}
