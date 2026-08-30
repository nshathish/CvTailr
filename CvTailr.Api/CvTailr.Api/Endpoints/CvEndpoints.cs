using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class CvEndpoints
{
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cv");

        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            group.RequireAuthorization();
        }

        group.MapPost("/parse", async Task<Results<Ok<CvDocument>, BadRequest<string>>> (
                ParseCvRequest request,
                ICvParsingService cvParsingService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.RawLatexSource))
                    return TypedResults.BadRequest("rawLatexSource is required.");

                var result = await cvParsingService.ParseAsync(request.RawLatexSource, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("ParseCv")
            .WithSummary("Parses raw LaTeX CV source into a structured CvDocument.");
    }
}

public record ParseCvRequest(string? RawLatexSource);
