using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Drill;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Ledger;
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
                ICvRepository cvRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                if (request.JdRequirements is null)
                    return TypedResults.BadRequest("jdRequirements is required.");

                var userId = currentUserContext.GetUserId();
                var cvDocument = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (cvDocument is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var question = await drillService.GenerateQuestionAsync(
                    cvDocument,
                    request.JdRequirements,
                    request.LedgerEntries ?? [],
                    cancellationToken);

                return TypedResults.Ok(question);
            })
            .WithName("GenerateDrillQuestion")
            .WithSummary("Generate Drill Question")
            .WithDescription(
                "Generates one interview drill question for the current user's CV, weighted toward provisional/gap items.");

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

public record GenerateQuestionRequest(JdRequirements? JdRequirements, List<LedgerEntry>? LedgerEntries);

public record EvaluateAnswerRequest(DrillQuestion? Question, string? AnswerText);
