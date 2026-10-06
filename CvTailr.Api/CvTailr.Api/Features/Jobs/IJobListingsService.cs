namespace CvTailr.Api.Features.Jobs;

/// <summary>
/// Shared job-listing matches ranked against a user's master CV, and adding a listing to a user's
/// own Jobs (task 026).
/// </summary>
public interface IJobListingsService
{
    /// <summary>Empty when the user has no master CV, or none of its skill tags overlap an active listing.</summary>
    Task<List<JobListingMatch>> GetMatchesAsync(string userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Null when the listing doesn't exist or fails the matches endpoint's activity rules.</summary>
    Task<AddToMyJobsOutcome?> AddToMyJobsAsync(string userId, string listingId, CancellationToken cancellationToken = default);
}

/// <summary>Never CanonicalUrl or SubmissionCount — those are internal to listing capture/de-duplication.</summary>
public record JobListingMatch(
    string ListingId,
    string RoleTitle,
    string CompanyName,
    string? CompanyDomain,
    string? Location,
    string? EmploymentType,
    string ApplyUrl,
    int MatchPercent,
    List<string> MatchedSkills,
    int RequirementCount,
    DateTimeOffset LastSeenAt);

public record AddToMyJobsOutcome(string JobId, bool AlreadyExists);
