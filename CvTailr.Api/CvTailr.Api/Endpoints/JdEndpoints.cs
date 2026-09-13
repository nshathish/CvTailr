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
            .WithDescription("Parses raw job description text into structured requirements via Azure AI Foundry.");

        group.MapPost("/parse", async Task<Results<Ok<Job>, BadRequest<string>>> (
                ParseJdRequest request,
                IJdParsingService jdParsingService,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.JdText))
                    return TypedResults.BadRequest("jdText is required.");

                var userId = currentUserContext.GetUserId();
                var jdRequirements = await jdParsingService.ParseAsync(request.JdText, cancellationToken);
                var job = await jobService.CreateFromJdAsync(userId, jdRequirements, cancellationToken);
                return TypedResults.Ok(job);
            })
            .WithName("ParseJd")
            .WithSummary("Parse JD")
            .WithDescription("Parses raw job description text into structured requirements and persists them as a new Job.");
    }
}

public record ParseJdRequest(string? JdText);