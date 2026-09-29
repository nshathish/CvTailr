using System.Security.Cryptography;
using System.Text;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Services;

/// <summary>
/// Captures a JobListing from a Job created via a URL, so future work (task 025) can show and
/// match against job postings without re-fetching/re-parsing them. Best effort: every failure is
/// caught and logged here — this must never fail or delay the caller's job creation, and never
/// retries inline. Never logs the submitting user's id alongside a listing id.
/// </summary>
public class JobListingCaptureService(
    IJobListingRepository jobListingRepository,
    ILogger<JobListingCaptureService> logger) : IJobListingCaptureService
{
    private const int MinRequirements = 3;

    public async Task CaptureAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.SourceUrl is null)
            return;

        var jd = job.JdRequirements;
        if (string.IsNullOrWhiteSpace(jd.RoleTitle) ||
            string.IsNullOrWhiteSpace(jd.CompanyName) ||
            jd.Requirements.Count < MinRequirements)
        {
            return;
        }

        string listingId;
        string canonicalUrl;
        string sourceHost;
        try
        {
            canonicalUrl = JobListingUrlHelper.Canonicalise(job.SourceUrl);
            listingId = ComputeListingId(canonicalUrl);
            sourceHost = new Uri(job.SourceUrl).Host;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JobListing capture: couldn't derive a canonical URL/id for SourceUrl '{SourceUrl}'.", job.SourceUrl);
            return;
        }

        try
        {
            var existing = await jobListingRepository.GetAsync(listingId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var metadata = job.SourceMetadata;

            var listing = new JobListing
            {
                Id = listingId,
                CanonicalUrl = canonicalUrl,
                ApplyUrl = ResolveApplyUrl(job.SourceUrl, metadata?.PostingUrl),
                SourceHost = sourceHost,
                RoleTitle = jd.RoleTitle!,
                CompanyName = jd.CompanyName!,
                Location = metadata?.Location,
                EmploymentType = metadata?.EmploymentType,
                DatePosted = metadata?.DatePosted,
                ValidThrough = metadata?.ValidThrough,
                Requirements = jd.Requirements,
                FirstSeenAt = existing?.FirstSeenAt ?? now,
                LastSeenAt = now,
                SubmissionCount = (existing?.SubmissionCount ?? 0) + 1,
                Status = JobListingStatus.Active
            };

            await jobListingRepository.UpsertAsync(listing, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JobListing capture failed for listing {ListingId} ({SourceHost}).", listingId, sourceHost);
        }
    }

    /// <summary>
    /// Prefers the JobPosting JSON-LD's own "url" when it's an absolute http/https URL on the same
    /// registrable domain as the final URL (guards against a listing aggregator's JSON-LD pointing
    /// at an unrelated domain); otherwise falls back to the final URL after redirects.
    /// </summary>
    private static string ResolveApplyUrl(string finalUrl, string? postingUrl)
    {
        if (!string.IsNullOrWhiteSpace(postingUrl) &&
            Uri.TryCreate(postingUrl, UriKind.Absolute, out var postingUri) &&
            (postingUri.Scheme == Uri.UriSchemeHttp || postingUri.Scheme == Uri.UriSchemeHttps) &&
            JobListingUrlHelper.IsSameRegistrableDomain(postingUri.Host, new Uri(finalUrl).Host))
        {
            return JobListingUrlHelper.CleanApplyUrl(postingUrl);
        }

        return JobListingUrlHelper.CleanApplyUrl(finalUrl);
    }

    private static string ComputeListingId(string canonicalUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl));
        return Convert.ToHexStringLower(hash);
    }
}
