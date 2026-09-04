using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Ledger;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ledger")
            .RequireAuthorization();

        group.MapPost("/register", async Task<Results<Ok<List<LedgerEntry>>, BadRequest<string>>> (
                RegisterLedgerRequest request,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                if (request.CvDocument is null)
                    return TypedResults.BadRequest("cvDocument is required.");

                var entries = await ledgerService.RegisterProvisionalItemsAsync(request.CvDocument, cancellationToken);
                return TypedResults.Ok(entries);
            })
            .WithName("RegisterProvisionalLedgerItems")
            .WithSummary("Registers ledger entries for provisional CV bullets/skills that don't already have one.");

        group.MapGet("/{cvId}", async Task<Ok<List<LedgerEntry>>> (
                string cvId,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                var entries = await ledgerService.GetByCvIdAsync(cvId, cancellationToken);
                return TypedResults.Ok(entries);
            })
            .WithName("GetLedgerEntriesForCv");

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
            .WithName("RecordLedgerDrillAttempt");

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
            .WithSummary("Read-only downgrade/promotion recommendation. Never mutates LedgerEntry.Status.");

        group.MapPost("/{entryId}/confirm-status", async Task<Results<Ok<LedgerEntry>, NotFound<string>>> (
                string entryId,
                ConfirmStatusChangeRequest request,
                ILedgerService ledgerService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var entry = await ledgerService.ConfirmStatusChangeAsync(entryId, request.NewStatus, cancellationToken);
                    return TypedResults.Ok(entry);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }
            })
            .WithName("ConfirmLedgerStatusChange")
            .WithSummary("Explicitly confirms a ledger status change. Must only be called after user confirmation — never automatically.");
    }
}

public record RegisterLedgerRequest(CvDocument? CvDocument);

public record RecordDrillAttemptRequest(DrillOutcome Outcome, string? Notes);

public record ConfirmStatusChangeRequest(LedgerStatus NewStatus);
