using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jd;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class JdEndpoints
{
    public static void MapJdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jd")
            .RequireAuthorization();

        group.MapPost("/parse", async Task<Results<Ok<JdRequirements>, BadRequest<string>>> (
                ParseJdRequest request,
                IJdParsingService jdParsingService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.JdText))
                    return TypedResults.BadRequest("jdText is required.");

                var result = await jdParsingService.ParseAsync(request.JdText, cancellationToken);
                return TypedResults.Ok(result);
            })
            .WithName("ParseJd")
            .WithSummary("Parses raw job description text into structured requirements.");
    }
}

public record ParseJdRequest(string? JdText);