using CvTailr.Api.Common.Auth;
using CvTailr.Api.Common.TailoredCv;
using CvTailr.Api.Features.Cv;
using CvTailr.Api.Features.Jobs.Listings;
using CvTailr.Api.Features.Ledger;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Features.Jobs;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs")
            .RequireAuthorization()
            .WithTags("Jobs")
            .WithDescription(
                "Reads persisted Jobs — one record per JD a user has parsed and scored/tailored against their CV.");

        group.MapPost("/parse",
            async Task<Results<Ok<Job>, BadRequest<JdParseError>, UnprocessableEntity<JdParseError>>> (
                ParseJdRequest request,
                IJdServiceClient jdServiceClient,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var hasText = !string.IsNullOrWhiteSpace(request.JdText);
                var hasUrl = !string.IsNullOrWhiteSpace(request.JdUrl);

                if (hasText == hasUrl)
                {
                    return TypedResults.BadRequest(
                        new JdParseError("InvalidInput", "Provide exactly one of jdText or jdUrl."));
                }

                // Delegates the actual parsing to the internal CvTailr.Jd.Api service (see
                // JdServiceClient) — this endpoint is the only thing Web/Mobile ever call for
                // turning a JD into a Job; Jd.Api itself is never exposed to them directly.
                var outcome = await jdServiceClient.ParseAsync(request.JdText, request.JdUrl, cancellationToken);
                if (outcome.Error is not null)
                    return TypedResults.UnprocessableEntity(new JdParseError(outcome.Error.Code, outcome.Error.Message));

                var result = outcome.Result!;
                var userId = currentUserContext.GetUserId();
                var job = await jobService.CreateFromJdAsync(
                    userId, result.JdRequirements, result.SourceUrl, result.SourceMetadata, cancellationToken);

                return TypedResults.Ok(job);
            })
            .WithName("ParseJd")
            .WithSummary("Parse JD")
            .WithDescription(
                "Parses a job description — either pasted text or a job posting URL — via the internal JD " +
                "parsing service, and persists the result as a new Job. Exactly one of jdText/jdUrl must be " +
                "provided.");

        group.MapGet("/", async Task<Ok<List<JobResponse>>> (
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var jobs = await jobService.GetResponsesForUserAsync(userId, cancellationToken);
                return TypedResults.Ok(jobs);
            })
            .WithName("GetJobs")
            .WithSummary("Get Jobs")
            .WithDescription("Lists the current user's persisted Jobs, most recently updated first.");

        group.MapGet("/{jobId}", async Task<Results<Ok<JobResponse>, NotFound<string>>> (
                string jobId,
                IJobService jobService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();
                var job = await jobService.GetResponseByIdAsync(userId, jobId, cancellationToken);
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

        group.MapPut("/{jobId}/details", async Task<Results<Ok<Job>, BadRequest<string>, NotFound<string>>> (
                string jobId,
                UpdateJobDetailsRequest request,
                IJobService jobService,
                IJobListingCaptureService jobListingCaptureService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var roleTitle = request.RoleTitle?.Trim();
                var companyName = request.CompanyName?.Trim();

                if (string.IsNullOrEmpty(roleTitle) || string.IsNullOrEmpty(companyName))
                    return TypedResults.BadRequest("roleTitle and companyName are both required.");

                // Always replaces the parse-time value — null when empty/missing or when the
                // given value turns out invalid/an excluded (job board/ATS/email/social) domain.
                var companyDomain = string.IsNullOrWhiteSpace(request.CompanyDomain)
                    ? null
                    : CompanyDomainResolver.Normalise(request.CompanyDomain);

                var userId = currentUserContext.GetUserId();

                Job job;
                try
                {
                    job = await jobService.UpdateDetailsAsync(
                        userId, jobId, roleTitle, companyName, companyDomain, cancellationToken);
                }
                catch (KeyNotFoundException ex)
                {
                    return TypedResults.NotFound(ex.Message);
                }

                // Best-effort — never fails or delays this response (see JobListingCaptureService).
                await jobListingCaptureService.CaptureAsync(job, cancellationToken);

                return TypedResults.Ok(job);
            })
            .WithName("UpdateJobDetails")
            .WithSummary("Update Job details")
            .WithDescription(
                "Saves the job title/company confirmed in the New Job wizard's review step and captures this " +
                "job's shared JobListing (best effort) when it came from a URL.");

        group.MapDelete("/{jobId}", async Task<Results<NoContent, NotFound<string>>> (
                string jobId,
                IJobService jobService,
                ICvRepository cvRepository,
                ITailoredCvRepository tailoredCvRepository,
                ILedgerRepository ledgerRepository,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();

                // Resolve cvId (the master CV's Id) BEFORE deleting the Job, since ledger
                // entries are scoped by (cvId, jobId) and this is the only place that id is
                // available — CvId is never stored on Job itself. CvDocument.Id is stable
                // across re-uploads, so this correctly matches whatever cvId this job's
                // ledger entries (if any) were registered under, even if the CV has since
                // been replaced.
                var masterCv = await cvRepository.GetByUserIdAsync(userId, cancellationToken);

                var deleted = await jobService.DeleteAsync(userId, jobId, cancellationToken);
                if (!deleted)
                    return TypedResults.NotFound($"Job '{jobId}' was not found for this user.");

                await tailoredCvRepository.DeleteByJobIdAsync(jobId, cancellationToken);

                if (masterCv is not null)
                    await ledgerRepository.DeleteByCvAndJobIdAsync(masterCv.Id, jobId, cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("DeleteJob")
            .WithSummary("Delete Job")
            .WithDescription(
                "Deletes a Job and cascades to its TailoredCvDocument and LedgerEntry records " +
                "scoped to that job. The master CvDocument is never touched.");
    }
}

public record JobCvResponse(TailoredCvDocument Document, bool IsTailored);

public record UpdateJobDetailsRequest(string? RoleTitle, string? CompanyName, string? CompanyDomain);

public record ParseJdRequest(string? JdText, string? JdUrl);

public record JdParseError(string Code, string Message);
