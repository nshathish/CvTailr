using CvTailr.Api.Exceptions;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

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

                var userId = currentUserContext.GetUserId();
                var job = await jobService.CreateFromJdAsync(userId, jdRequirements, source.SourceUrl, cancellationToken);
                return TypedResults.Ok(job);
            })
            .WithName("ParseJd")
            .WithSummary("Parse JD")
            .WithDescription(
                "Parses a job description — either pasted text or a job posting URL — into structured requirements " +
                "and persists them as a new Job. Exactly one of jdText/jdUrl must be provided.");
    }
}

public record ParseJdRequest(string? JdText, string? JdUrl);

public record JdParseError(string Code, string Message);
