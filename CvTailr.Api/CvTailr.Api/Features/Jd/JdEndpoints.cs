using CvTailr.Api.Common.Auth;
using CvTailr.Api.Features.Jobs;
using CvTailr.Api.Features.Jobs.Listings;
using CvTailr.Shared.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Features.Jd;

public static class JdEndpoints
{
    public static void MapJdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jd")
            .RequireAuthorization()
            .WithTags("Jd")
            .WithDescription("Parses a job description (pasted text or a URL) into structured requirements using an LLM.");

        group.MapPost("/parse", async Task<Results<Ok<Job>, BadRequest<JdParseError>, UnprocessableEntity<JdParseError>>> (
                ParseJdRequest request,
                IJdSourceResolver jdSourceResolver,
                IJdParsingService jdParsingService,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var hasText = !string.IsNullOrWhiteSpace(request.JdText);
                var hasUrl = !string.IsNullOrWhiteSpace(request.JdUrl);

                if (hasText == hasUrl)
                {
                    return TypedResults.BadRequest(
                        new JdParseError("InvalidInput", "Provide exactly one of jdText or jdUrl."));
                }

                JdSource source;
                try
                {
                    source = await jdSourceResolver.ResolveAsync(request.JdText, request.JdUrl, cancellationToken);
                }
                catch (JdUrlException ex)
                {
                    return TypedResults.UnprocessableEntity(new JdParseError(ex.ErrorCode, ex.Message));
                }

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

                var userId = currentUserContext.GetUserId();
                var job = await jobService.CreateFromJdAsync(
                    userId, jdRequirements, source.SourceUrl, sourceMetadata, cancellationToken);

                return TypedResults.Ok(job);
            })
            .WithName("ParseJd")
            .WithSummary("Parse JD")
            .WithDescription(
                "Parses a job description — either pasted text or a job posting URL — into structured requirements " +
                "and persists them as a new Job. Exactly one of jdText/jdUrl must be provided.");
    }

    private static bool HasAnySourceMetadata(JdSource source) =>
        source.Location is not null || source.EmploymentType is not null ||
        source.DatePosted is not null || source.ValidThrough is not null || source.PostingUrl is not null;
}

public record ParseJdRequest(string? JdText, string? JdUrl);

public record JdParseError(string Code, string Message);
