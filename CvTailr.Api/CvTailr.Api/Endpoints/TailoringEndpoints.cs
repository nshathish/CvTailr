using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using CvTailr.Shared.Tailoring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class TailoringEndpoints
{
    public static void MapTailoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tailor")
            .RequireAuthorization();

        group.MapPost("/propose", async Task<Results<Ok<TailoringProposal>, BadRequest<string>, NotFound<string>>> (
                TailorProposeRequest request,
                ITailoringService tailoringService,
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

                var result = await tailoringService.ProposeAsync(
                    cvDocument,
                    request.JdRequirements,
                    request.MatchScoreResult,
                    cancellationToken);

                return TypedResults.Ok(result);
            })
            .WithName("ProposeTailoring")
            .WithSummary("Proposes CV tailoring changes for a JD without persisting or mutating anything.");

        group.MapPost("/apply", async Task<Results<Ok<TailorApplyResult>, BadRequest<string>, NotFound<string>>> (
                TailorApplyRequest request,
                ITailoringService tailoringService,
                ICvRepository cvRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var cvDocument = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (cvDocument is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var result = tailoringService.ApplyAsync(
                    cvDocument,
                    request.ApprovedBulletRewriteIds ?? [],
                    request.ProposedBulletRewrites ?? [],
                    request.ApprovedNewBullets ?? [],
                    request.ApprovedNewSkills ?? []);

                // Persist the updated document — this is what unblocks future server-side stripping
                // of Removed-status provisional content (root CLAUDE.md's "Known open design
                // decisions"): CvDocument is no longer stateless, so there's now something to write
                // that stripping logic back to. Not implemented here — out of scope for this task.
                await cvRepository.UpsertAsync(result.CvDocument, cancellationToken);

                return TypedResults.Ok(result);
            })
            .WithName("ApplyTailoring")
            .WithSummary("Applies user-approved tailoring changes to the current user's CvDocument, marking new content provisional, and persists the result.");
    }
}

public record TailorProposeRequest(
    JdRequirements? JdRequirements,
    MatchScoreResult? MatchScoreResult);

public record TailorApplyRequest(
    List<string>? ApprovedBulletRewriteIds,
    List<BulletRewriteProposal>? ProposedBulletRewrites,
    List<NewBulletProposal>? ApprovedNewBullets,
    List<NewSkillProposal>? ApprovedNewSkills);
