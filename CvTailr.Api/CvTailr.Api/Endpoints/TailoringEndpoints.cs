using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Tailoring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class TailoringEndpoints
{
    public static void MapTailoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tailor")
            .RequireAuthorization()
            .WithTags("Tailor")
            .WithDescription(
                "Proposes and applies CV tailoring changes against a Job's JD. Proposals never mutate or persist anything — only an explicit apply call, after user approval, updates the CvDocument.");

        group.MapPost("/propose", async Task<Results<Ok<TailoringProposal>, BadRequest<string>, NotFound<string>>> (
                TailorProposeRequest request,
                ITailoringService tailoringService,
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

                var result = await tailoringService.ProposeAsync(
                    cvDocument,
                    job.JdRequirements,
                    job.MatchScoreResult,
                    cancellationToken);

                return TypedResults.Ok(result);
            })
            .WithName("ProposeTailoring")
            .WithSummary("Propose Tailoring")
            .WithDescription("Proposes CV tailoring changes for a Job's JD without persisting or mutating anything.");

        group.MapPost("/apply", async Task<Results<Ok<TailorApplyResponse>, BadRequest<string>, NotFound<string>>> (
                TailorApplyRequest request,
                ITailoringService tailoringService,
                ICvRepository cvRepository,
                ILedgerService ledgerService,
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

                // Ledger registration and the Job's Tailored transition happen as part of the same
                // apply call so the client never has to orchestrate three separate requests for what
                // is conceptually one action.
                await ledgerService.RegisterProvisionalItemsAsync(result.CvDocument, cancellationToken);
                var updatedJob = await jobService.MarkTailoredAsync(userId, request.JobId, cancellationToken);

                return TypedResults.Ok(new TailorApplyResponse(result.CvDocument, updatedJob, result.Warnings));
            })
            .WithName("ApplyTailoring")
            .WithSummary("Apply Tailoring")
            .WithDescription(
                "Applies user-approved tailoring changes to the current user's CvDocument, persists it, registers ledger entries for new provisional content, and marks the Job Tailored.");
    }
}

public record TailorProposeRequest(string? JobId);

public record TailorApplyRequest(
    string? JobId,
    List<string>? ApprovedBulletRewriteIds,
    List<BulletRewriteProposal>? ProposedBulletRewrites,
    List<NewBulletProposal>? ApprovedNewBullets,
    List<NewSkillProposal>? ApprovedNewSkills);

public record TailorApplyResponse(CvDocument CvDocument, Job Job, List<string> Warnings);
