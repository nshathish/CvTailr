namespace CvTailr.Jd.Api.Domain;

public record JdSource(
    string Text,
    string? RoleTitle,
    string? CompanyName,
    string? SourceUrl,
    string? Location,
    string? EmploymentType,
    DateTimeOffset? DatePosted,
    DateTimeOffset? ValidThrough,
    string? PostingUrl,
    List<string> HiringOrganizationUrls);
