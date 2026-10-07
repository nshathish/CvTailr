using CvTailr.Jd.Api.Application;
using CvTailr.Jd.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Jd.Api.Endpoints;

public static class JdEndpoints
{
    public static void MapJdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jd")
            .RequireAuthorization()
            .WithTags("Jd")
            .WithDescription(
                "Parses a job description (pasted text or a URL) into structured requirements using an LLM.");

        group.MapPost("/parse",
            async Task<Results<Ok<JdParseResult>, BadRequest<JdParseError>, UnprocessableEntity<JdParseError>>> (
                ParseJdRequest request,
                IJdParseOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
            {
                // same hasText/hasUrl validation JdEndpoints.cs has today
                try
                {
                    var result = await orchestrator.ParseAsync(request.JdText, request.JdUrl, cancellationToken);
                    return TypedResults.Ok(result);
                }
                catch (JdUrlException ex)
                {
                    return TypedResults.UnprocessableEntity(new JdParseError(ex.ErrorCode, ex.Message));
                }
            });
    }
}