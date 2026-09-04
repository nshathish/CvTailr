using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class ScoringEndpoints
{
    public static void MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/score")
            .RequireAuthorization();

        group.MapPost("/", async Task<Results<Ok<MatchScoreResult>, BadRequest<string>, NotFound<string>>> (
                ScoreRequest request,
                IScoringService scoringService,
                ICvRepository cvRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                if (request.JdRequirements is null)
                    return TypedResults.BadRequest("jdRequirements is required.");

                if (request.JdRequirements.Requirements.Count == 0)
                    return TypedResults.BadRequest("jdRequirements.requirements must be non-empty.");

                var userId = currentUserContext.GetUserId();
                var cvDocument = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (cvDocument is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var result = await scoringService.ScoreAsync(request.JdRequirements, cvDocument, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("ScoreCv")
            .WithSummary("Scores the current user's persisted CvDocument against JdRequirements and returns a MatchScoreResult.");
    }
}

public record ScoreRequest(JdRequirements? JdRequirements);
