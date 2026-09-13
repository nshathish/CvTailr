using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class CvEndpoints
{
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cv")
            .RequireAuthorization()
            .WithTags("Cv")
            .WithDescription(
                "Parses a candidate's raw LaTeX CV into a structured CvDocument and persists exactly one current CV per user.");

        group.MapPost("/parse", async Task<Results<Ok<CvDocument>, BadRequest<string>>> (
                UploadCvRequest request,
                ICvParsingService cvParsingService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.RawLatexSource))
                    return TypedResults.BadRequest("rawLatexSource is required.");

                var userId = currentUserContext.GetUserId();
                var result = await cvParsingService.UploadAndParseAsync(userId, request.RawLatexSource, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("UploadCv")
            .WithSummary("Upload CV")
            .WithDescription(
                "Uploads/replaces the current user's CV: parses raw LaTeX source into a structured CvDocument and persists it.");

        group.MapGet("/current", async Task<Results<Ok<CvDocument>, NotFound<string>>> (
                ICvParsingService cvParsingService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var document = await cvParsingService.GetCurrentAsync(userId, cancellationToken);
                return document is null
                    ? TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.")
                    : TypedResults.Ok(document);
            })
            .WithName("GetCurrentCv")
            .WithSummary("Get Current CV")
            .WithDescription("Returns the current user's persisted CvDocument.");
    }
}

public record UploadCvRequest(string? RawLatexSource);
