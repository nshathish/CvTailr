using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class ScoringEndpoints
{
    public static void MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/score")
            .RequireAuthorization()
            .WithTags("Score")
            .WithDescription("Scores the current user's persisted CV against a Job's parsed requirements.");

        group.MapPost("/", async Task<Results<Ok<Job>, BadRequest<string>, NotFound<string>>> (
                ScoreRequest request,
                IScoringService scoringService,
                ICvRepository cvRepository,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.JobId))
                    return TypedResults.BadRequest("jobId is required.");

                var userId = currentUserContext.GetUserId();

                var job = await jobService.GetByIdAsync(userId, request.JobId, cancellationToken);
                if (job is null)
                    return TypedResults.NotFound($"Job '{request.JobId}' was not found for this user.");

                var cvDocument = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (cvDocument is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var result = await scoringService.ScoreAsync(job.JdRequirements, cvDocument, cancellationToken);
                var updatedJob = await jobService.AttachScoreAsync(userId, request.JobId, result, cancellationToken);
                return TypedResults.Ok(updatedJob);
            })
            .WithName("ScoreCv")
            .WithSummary("Score CV")
            .WithDescription(
                "Scores the current user's persisted CvDocument against a Job's JdRequirements, attaches the result to the Job, and returns it.");
    }
}

public record ScoreRequest(string? JobId);
