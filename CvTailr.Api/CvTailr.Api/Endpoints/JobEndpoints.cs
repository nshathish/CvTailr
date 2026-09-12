using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs")
            .RequireAuthorization()
            .WithTags("Jobs")
            .WithDescription(
                "Reads persisted Jobs — one record per JD a user has parsed and scored/tailored against their CV.");

        group.MapGet("/", async Task<Ok<List<Job>>> (
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var jobs = await jobService.GetAllForUserAsync(userId, cancellationToken);
                return TypedResults.Ok(jobs);
            })
            .WithName("GetJobs")
            .WithSummary("Get Jobs")
            .WithDescription("Lists the current user's persisted Jobs, most recently updated first.");

        group.MapGet("/{jobId}", async Task<Results<Ok<Job>, NotFound<string>>> (
                string jobId,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var job = await jobService.GetByIdAsync(userId, jobId, cancellationToken);
                return job is null
                    ? TypedResults.NotFound($"Job '{jobId}' was not found for this user.")
                    : TypedResults.Ok(job);
            })
            .WithName("GetJobById")
            .WithSummary("Get Job by Id")
            .WithDescription("Returns a single persisted Job for the current user.");
    }
}
