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
                ITailoredCvRepository tailoredCvRepository,
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

                var baseDocument = await ResolveBaseDocumentAsync(
                    userId, request.JobId, cvRepository, tailoredCvRepository, cancellationToken);
                if (baseDocument is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var result = await tailoringService.ProposeAsync(
                    baseDocument,
                    job.JdRequirements,
                    job.MatchScoreResult,
                    cancellationToken);

                return TypedResults.Ok(result);
            })
            .WithName("ProposeTailoring")
            .WithSummary("Propose Tailoring")
            .WithDescription(
                "Proposes CV tailoring changes for a Job's JD without persisting or mutating anything. Bases the " +
                "proposal on this job's already-tailored CV if one exists, otherwise on the master CV.");

        group.MapPost("/apply", async Task<Results<Ok<TailorApplyResponse>, BadRequest<string>, NotFound<string>>> (
                TailorApplyRequest request,
                ITailoringService tailoringService,
                ICvRepository cvRepository,
                ITailoredCvRepository tailoredCvRepository,
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

                var existingTailoredCv = await tailoredCvRepository.GetByJobIdAsync(request.JobId, cancellationToken);

                CvDocument baseDocument;
                if (existingTailoredCv is not null)
                {
                    baseDocument = ToCvDocument(existingTailoredCv);
                }
                else
                {
                    var masterCv = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                    if (masterCv is null)
                        return TypedResults.NotFound(
                            "No CV found for this user — upload one via /api/cv/upload first.");

                    baseDocument = masterCv;
                }

                var result = tailoringService.ApplyAsync(
                    baseDocument,
                    request.ApprovedBulletRewriteIds ?? [],
                    request.ProposedBulletRewrites ?? [],
                    request.ApprovedNewBullets ?? [],
                    request.ApprovedNewSkills ?? []);

                // Persist the result as a per-job TailoredCvDocument — never back into the master
                // CvDocument/ICvRepository. The master CV is only ever created/replaced via
                // /api/cv/upload, so different jobs' tailoring never interferes with each other or
                // with the original.
                var tailoredCvDocument = existingTailoredCv ?? new TailoredCvDocument
                {
                    UserId = userId,
                    CvId = baseDocument.Id,
                    JobId = request.JobId
                };
                tailoredCvDocument.Roles = result.CvDocument.Roles;
                tailoredCvDocument.Skills = result.CvDocument.Skills;
                tailoredCvDocument.UpdatedAt = DateTimeOffset.UtcNow;

                await tailoredCvRepository.UpsertAsync(tailoredCvDocument, cancellationToken);

                // Ledger registration and the Job's Tailored transition happen as part of the same
                // apply call so the client never has to orchestrate three separate requests for what
                // is conceptually one action.
                await ledgerService.RegisterProvisionalItemsAsync(
                    tailoredCvDocument.CvId, request.JobId, tailoredCvDocument, cancellationToken);
                var updatedJob = await jobService.MarkTailoredAsync(userId, request.JobId, cancellationToken);

                return TypedResults.Ok(new TailorApplyResponse(tailoredCvDocument, updatedJob, result.Warnings));
            })
            .WithName("ApplyTailoring")
            .WithSummary("Apply Tailoring")
            .WithDescription(
                "Applies user-approved tailoring changes on top of this job's existing tailored CV (or the master " +
                "CV if none exists yet), persists the result as that job's TailoredCvDocument, registers ledger " +
                "entries for new provisional content, and marks the Job Tailored. Never modifies the master CV.");
    }

    /// <summary>
    /// Resolves the document tailoring should be based on for a given job: the job's own
    /// TailoredCvDocument if one already exists (so re-tailoring builds on prior edits, not from
    /// scratch), otherwise the user's master CvDocument. Returns null if the user has no master CV
    /// at all and no tailoring has happened yet for this job.
    /// </summary>
    private static async Task<CvDocument?> ResolveBaseDocumentAsync(
        string userId,
        string jobId,
        ICvRepository cvRepository,
        ITailoredCvRepository tailoredCvRepository,
        CancellationToken cancellationToken)
    {
        var tailoredCv = await tailoredCvRepository.GetByJobIdAsync(jobId, cancellationToken);
        if (tailoredCv is not null)
            return ToCvDocument(tailoredCv);

        return await cvRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>
    /// TailoredCvDocument and CvDocument share the same Roles/Skills shape, so ITailoringService
    /// (which operates on "a document" generically) can work against either without duplicating any
    /// tailoring logic — this just adapts the shape.
    /// </summary>
    private static CvDocument ToCvDocument(TailoredCvDocument tailoredCvDocument) => new()
    {
        Id = tailoredCvDocument.CvId,
        UserId = tailoredCvDocument.UserId,
        Roles = tailoredCvDocument.Roles,
        Skills = tailoredCvDocument.Skills
    };
}

public record TailorProposeRequest(string? JobId);

public record TailorApplyRequest(
    string? JobId,
    List<string>? ApprovedBulletRewriteIds,
    List<BulletRewriteProposal>? ProposedBulletRewrites,
    List<NewBulletProposal>? ApprovedNewBullets,
    List<NewSkillProposal>? ApprovedNewSkills);

public record TailorApplyResponse(TailoredCvDocument TailoredCvDocument, Job Job, List<string> Warnings);