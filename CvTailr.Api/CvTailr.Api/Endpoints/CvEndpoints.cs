using CvTailr.Api.Services;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CvTailr.Api.Endpoints;

public static class CvEndpoints
{
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cv")
            .RequireAuthorization()
            .WithTags("Cv")
            .WithDescription(
                "Parses a candidate's CV (uploaded file or pasted text) into a structured CvDocument and persists exactly one current CV per user.");

        group.MapPost("/parse", async Task<Results<Ok<CvDocument>, BadRequest<string>>> (
                IFormFile? CvFile,
                [FromForm] string? RawCvText,
                ICvSourceExtractor cvSourceExtractor,
                ICvParsingService cvParsingService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var hasFile = CvFile is not null;
                var hasText = !string.IsNullOrWhiteSpace(RawCvText);

                if (!hasFile && !hasText)
                    return TypedResults.BadRequest("either cvFile or rawCvText is required.");

                if (hasFile && hasText)
                    return TypedResults.BadRequest("provide either cvFile or rawCvText, not both.");

                string rawCvSource;
                if (hasFile)
                {
                    try
                    {
                        await using var stream = CvFile!.OpenReadStream();
                        rawCvSource = await cvSourceExtractor.ExtractAsync(stream, CvFile.FileName, cancellationToken);
                    }
                    catch (UnsupportedCvFormatException ex)
                    {
                        return TypedResults.BadRequest(ex.Message);
                    }
                }
                else
                {
                    rawCvSource = RawCvText!;
                }

                var userId = currentUserContext.GetUserId();
                var result = await cvParsingService.UploadAndParseAsync(userId, rawCvSource, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("UploadCv")
            .WithSummary("Upload CV")
            .WithDescription(
                "Uploads/replaces the current user's CV: extracts text from an uploaded file (.pdf/.docx/.txt/.tex) or " +
                "uses pasted text directly, parses it into a structured CvDocument via the existing Foundry step, and " +
                "persists it.")
            .DisableAntiforgery();

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
