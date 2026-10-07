namespace CvTailr.Jd.Api.Domain.CompanyDomain;

/// <summary>
/// Registrable domains that are never a company's own domain — job board/ATS hosts, generic
/// email providers, and social/directory sites that can appear in a JobPosting's
/// hiringOrganization.sameAs. Compared against <see cref="JobListingUrlHelper.GetRegistrableDomain"/>'s
/// output; see <see cref="CompanyDomainResolver"/>.
/// </summary>
public static class ExcludedCompanyDomains
{
    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        // Job boards and ATS hosts
        "greenhouse.io", "lever.co", "workable.com", "ashbyhq.com", "myworkdayjobs.com",
        "workday.com", "smartrecruiters.com", "teamtailor.com", "bamboohr.com", "recruitee.com",
        "personio.de", "pinpointhq.com", "linkedin.com", "indeed.com", "glassdoor.com",
        "glassdoor.co.uk", "totaljobs.com", "reed.co.uk", "cv-library.co.uk", "otta.com",
        "welcometothejungle.com",

        // Generic email domains
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "yahoo.com",
        "icloud.com", "proton.me", "protonmail.com",

        // Social and directory sites (for sameAs)
        "twitter.com", "x.com", "facebook.com", "instagram.com", "youtube.com", "github.com",
        "crunchbase.com", "wikipedia.org"
    };

    public static bool IsExcluded(string? registrableDomain) =>
        registrableDomain is not null && Domains.Contains(registrableDomain);
}
