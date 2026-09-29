namespace CvTailr.Shared.Jobs;

/// <summary>
/// A user-independent cache of a job posting captured when a user creates a Job from a URL (see
/// CvTailr.Api's JobListingCaptureService). Holds only the structured facts needed to later show
/// and score the listing, plus a link to the original posting — never a user id, CV data, match
/// scores, evidence, raw JD text, or HTML.
/// </summary>
public class JobListing
{
    /// <summary>Lowercase hex SHA-256 of CanonicalUrl.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>De-duplication key only — never shown to users.</summary>
    public string CanonicalUrl { get; set; } = string.Empty;

    /// <summary>The link shown to users for opening the original posting.</summary>
    public string ApplyUrl { get; set; } = string.Empty;

    public string SourceHost { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>The company's own website domain (e.g. "monzo.com"), or null when not
    /// confidently determined.</summary>
    public string? CompanyDomain { get; set; }

    public string? Location { get; set; }
    public string? EmploymentType { get; set; }
    public DateTimeOffset? DatePosted { get; set; }
    public DateTimeOffset? ValidThrough { get; set; }
    public List<JobListingRequirement> Requirements { get; set; } = [];

    /// <summary>The distinct union of all Requirements[].Tags — matched against a user's CvDocument.SkillTags.</summary>
    public List<string> SkillTags { get; set; } = [];

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public int SubmissionCount { get; set; }
    public JobListingStatus Status { get; set; } = JobListingStatus.Active;
}
