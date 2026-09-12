using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Ledger;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ledger")
            .RequireAuthorization()
            .WithTags("Ledger")
            .WithDescription(
                "Tracks provisional CV content (bullets/skills without evidence), scoped per (cvId, jobId), and gates status changes behind explicit user confirmation — never automatic.");

        group.MapPost("/register", async Task<Results<Ok<List<LedgerEntry>>, BadRequest<string>, NotFound<string>>> (
                RegisterLedgerRequest request,
                ILedgerService ledgerService,
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

                var tailoredDocument = await tailoredCvRepository.GetByJobIdAsync(request.JobId, cancellationToken);
                if (tailoredDocument is null)
                    return TypedResults.NotFound(
                        $"No tailored CV exists yet for job '{request.JobId}' — apply tailoring via /api/tailor/apply first.");

                var entries = await ledgerService.RegisterProvisionalItemsAsync(
                    tailoredDocument.CvId, request.JobId, tailoredDocument, cancellationToken);

                return TypedResults.Ok(entries);
            })
            .WithName("RegisterProvisionalLedgerItems")
            .WithSummary("Register Provisional Ledger Items")
            .WithDescription(
                "Registers ledger entries for a job's tailored CV's provisional bullets/skills that don't already have one.");

        group.MapGet("/{cvId}", async Task<Ok<List<LedgerEntry>>> (
                string cvId,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                var entries = await ledgerService.GetByCvIdAsync(cvId, cancellationToken);
                return TypedResults.Ok(entries);
            })
            .WithName("GetLedgerEntriesForCv")
            .WithSummary("Get Ledger Entries for CV")
            .WithDescription("Returns all ledger entries across every job tailored against this CV.");

        group.MapGet("/jobs/{jobId}", async Task<Results<Ok<List<LedgerEntry>>, NotFound<string>>> (
                string jobId,
                ILedgerService ledgerService,
                IJobService jobService,
                ICvRepository cvRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();

                var job = await jobService.GetByIdAsync(userId, jobId, cancellationToken);
                if (job is null)
                    return TypedResults.NotFound($"Job '{jobId}' was not found for this user.");

                var masterCv = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (masterCv is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var entries = await ledgerService.GetByCvAndJobIdAsync(masterCv.Id, jobId, cancellationToken);
                return TypedResults.Ok(entries);
            })
            .WithName("GetLedgerEntriesForJob")
            .WithSummary("Get Ledger Entries for Job")
            .WithDescription("Returns ledger entries scoped to one specific job's tailoring only.");

        group.MapPost("/{entryId}/drill-attempt", async Task<Results<Ok<LedgerEntry>, NotFound<string>>> (
                string entryId,
                RecordDrillAttemptRequest request,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                var attempt = new DrillAttempt
                {
                    Outcome = request.Outcome,
                    Notes = request.Notes,
                    Timestamp = DateTimeOffset.UtcNow
                };

                try
                {
                    var entry = await ledgerService.RecordDrillAttemptAsync(entryId, attempt, cancellationToken);
                    return TypedResults.Ok(entry);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }
            })
            .WithName("RecordLedgerDrillAttempt")
            .WithSummary("Record Ledger Drill Attempt")
            .WithDescription("Records a drill attempt for a ledger entry and returns the updated entry.");

        group.MapGet("/{entryId}/review", async Task<Results<Ok<LedgerReviewRecommendation>, NotFound<string>>> (
                string entryId,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var recommendation = await ledgerService.ReviewAsync(entryId, cancellationToken);
                    return TypedResults.Ok(recommendation);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }
            })
            .WithName("ReviewLedgerEntry")
            .WithSummary("Review Ledger Entry")
            .WithDescription("Read-only downgrade/promotion recommendation. Never mutates LedgerEntry.Status.");

        group.MapPost("/{entryId}/confirm-status", async Task<Results<Ok<LedgerEntry>, NotFound<string>>> (
                string entryId,
                ConfirmStatusChangeRequest request,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var entry = await ledgerService.ConfirmStatusChangeAsync(entryId, request.NewStatus,
                        cancellationToken);
                    return TypedResults.Ok(entry);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }
            })
            .WithName("ConfirmLedgerStatusChange")
            .WithSummary("Confirm Ledger Status Change")
            .WithDescription(
                "Explicitly confirms a ledger status change. Must only be called after user confirmation — never automatically. " +
                "Confirmed/Removed also update the job's own TailoredCvDocument, never the master CvDocument.");
    }
}

public record RegisterLedgerRequest(string? JobId);

public record RecordDrillAttemptRequest(DrillOutcome Outcome, string? Notes);

public record ConfirmStatusChangeRequest(LedgerStatus NewStatus);
