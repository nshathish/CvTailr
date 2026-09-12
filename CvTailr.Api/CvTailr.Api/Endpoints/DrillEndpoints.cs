using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Drill;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class DrillEndpoints
{
    public static void MapDrillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/drill")
            .RequireAuthorization()
            .WithTags("Drill")
            .WithDescription(
                "Generates interview drill questions weighted toward provisional/gap items and evaluates answers, feeding weak/fail outcomes back into the ledger.");

        group.MapPost("/question", async Task<Results<Ok<DrillQuestion>, BadRequest<string>, NotFound<string>>> (
                GenerateQuestionRequest request,
                IDrillService drillService,
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

                try
                {
                    var question = await drillService.GenerateQuestionAsync(
                        userId, request.JobId, job.JdRequirements, cancellationToken);
                    return TypedResults.Ok(question);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }
            })
            .WithName("GenerateDrillQuestion")
            .WithSummary("Generate Drill Question")
            .WithDescription(
                "Generates one interview drill question scoped to a single job's tailored CV and ledger history, weighted toward provisional/gap items.");

        group.MapPost("/answer", async Task<Results<Ok<DrillAnswerEvaluation>, BadRequest<string>>> (
                EvaluateAnswerRequest request,
                IDrillService drillService,
                CancellationToken cancellationToken) =>
            {
                if (request.Question is null)
                    return TypedResults.BadRequest("question is required.");

                if (string.IsNullOrWhiteSpace(request.AnswerText))
                    return TypedResults.BadRequest("answerText is required.");

                var evaluation = await drillService.EvaluateAnswerAsync(request.Question, request.AnswerText, cancellationToken);
                return TypedResults.Ok(evaluation);
            })
            .WithName("EvaluateDrillAnswer")
            .WithSummary("Evaluate Drill Answer")
            .WithDescription(
                "Evaluates a typed or transcribed drill answer and records the outcome to the ledger when applicable.");
    }
}

public record GenerateQuestionRequest(string? JobId);

public record EvaluateAnswerRequest(DrillQuestion? Question, string? AnswerText);
