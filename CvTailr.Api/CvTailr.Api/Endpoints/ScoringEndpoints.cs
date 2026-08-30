using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class ScoringEndpoints
{
    public static void MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/score");

        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            group.RequireAuthorization();
        }

        group.MapPost("/", async Task<Results<Ok<MatchScoreResult>, BadRequest<string>>> (
                ScoreRequest request,
                IScoringService scoringService,
                CancellationToken cancellationToken) =>
            {
                if (request.JdRequirements is null || request.CvDocument is null)
                    return TypedResults.BadRequest("jdRequirements and cvDocument are both required.");

                if (request.JdRequirements.Requirements.Count == 0)
                    return TypedResults.BadRequest("jdRequirements.requirements must be non-empty.");

                var result = await scoringService.ScoreAsync(request.JdRequirements, request.CvDocument, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("ScoreCv")
            .WithSummary("Scores a CvDocument against JdRequirements and returns a MatchScoreResult.");
    }
}

public record ScoreRequest(JdRequirements? JdRequirements, CvDocument? CvDocument);
