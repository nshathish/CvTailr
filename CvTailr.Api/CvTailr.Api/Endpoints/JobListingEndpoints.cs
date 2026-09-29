using CvTailr.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CvTailr.Api.Endpoints;

public static class JobListingEndpoints
{
    private const int DefaultMatchesLimit = 6;
    private const int MaxMatchesLimit = 50;

    public static void MapJobListingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/job-listings")
            .RequireAuthorization()
            .WithTags("JobListings")
            .WithDescription(
                "Shared job listings ranked against the current user's master CV, and adding one to the user's own Jobs.");

        group.MapGet("/matches", async Task<Ok<List<JobListingMatch>>> (
                int? limit,
                IJobListingsService jobListingsService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var effectiveLimit = Math.Clamp(limit ?? DefaultMatchesLimit, 1, MaxMatchesLimit);

                var userId = currentUserContext.GetUserId();
                var matches = await jobListingsService.GetMatchesAsync(userId, effectiveLimit, cancellationToken);
                return TypedResults.Ok(matches);
            })
            .WithName("GetJobListingMatches")
            .WithSummary("Get Job Listing Matches")
            .WithDescription(
                "Active shared job listings with at least 40% match against the user's master CV skill tags, " +
                "ranked highest first. Empty when the user has no master CV.");

        group.MapPost("/{listingId}/add-to-my-jobs", async Task<Results<Created<AddToMyJobsResponse>, Ok<AddToMyJobsResponse>, NotFound<string>>> (
                string listingId,
                IJobListingsService jobListingsService,
                ICurrentUserContext currentUserContext,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUserContext.GetUserId();

                var outcome = await jobListingsService.AddToMyJobsAsync(userId, listingId, cancellationToken);
                if (outcome is null)
                    return TypedResults.NotFound($"Job listing '{listingId}' was not found or is no longer available.");

                var response = new AddToMyJobsResponse(outcome.JobId);
                return outcome.AlreadyExists
                    ? TypedResults.Ok(response)
                    : TypedResults.Created($"/api/jobs/{outcome.JobId}", response);
            })
            .WithName("AddJobListingToMyJobs")
            .WithSummary("Add Job Listing To My Jobs")
            .WithDescription(
                "Creates a scored Job for the current user from a shared job listing (200 + existing JobId if " +
                "the user already added it, 201 + new JobId otherwise). Never modifies the listing itself.");
    }
}

public record AddToMyJobsResponse(string JobId);
