using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
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

        group.MapGet("/{jobId}/cv", async Task<Results<Ok<JobCvResponse>, NotFound<string>>> (
                string jobId,
                IJobService jobService,
                ICvRepository cvRepository,
                ITailoredCvRepository tailoredCvRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();

                var job = await jobService.GetByIdAsync(userId, jobId, cancellationToken);
                if (job is null)
                    return TypedResults.NotFound($"Job '{jobId}' was not found for this user.");

                var tailoredCv = await tailoredCvRepository.GetByJobIdAsync(jobId, cancellationToken);
                if (tailoredCv is not null)
                    return TypedResults.Ok(new JobCvResponse(tailoredCv, IsTailored: true));

                var masterCv = await cvRepository.GetByUserIdAsync(userId, cancellationToken);
                if (masterCv is null)
                    return TypedResults.NotFound("No CV found for this user — upload one via /api/cv/upload first.");

                var asTailoredShape = new TailoredCvDocument
                {
                    Id = masterCv.Id,
                    UserId = masterCv.UserId,
                    CvId = masterCv.Id,
                    JobId = jobId,
                    Roles = masterCv.Roles,
                    Skills = masterCv.Skills
                };

                return TypedResults.Ok(new JobCvResponse(asTailoredShape, IsTailored: false));
            })
            .WithName("GetJobCv")
            .WithSummary("Get Job CV")
            .WithDescription(
                "Returns this job's tailored CV if one exists (IsTailored: true), otherwise the master CV as a " +
                "preview of what would be tailored (IsTailored: false). The master CV is never modified.");
    }
}

public record JobCvResponse(TailoredCvDocument Document, bool IsTailored);
