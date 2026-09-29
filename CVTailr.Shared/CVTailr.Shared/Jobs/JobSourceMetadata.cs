namespace CvTailr.Shared.Jobs;

/// <summary>
/// JSON-LD extras captured from a job posting URL (task 020) and carried on the Job purely to
/// feed JobListing capture (task 022) once the user confirms the review-step title/company —
/// not otherwise surfaced in the Web UI.
/// </summary>
public class JobSourceMetadata
{
    public string? Location { get; set; }
    public string? EmploymentType { get; set; }
    public DateTimeOffset? DatePosted { get; set; }
    public DateTimeOffset? ValidThrough { get; set; }
    public string? PostingUrl { get; set; }
}
