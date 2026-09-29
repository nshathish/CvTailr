using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Services.Interfaces;

/// <summary>
/// Best-effort capture of a user-independent JobListing when a Job is created from a URL (task
/// 022). Never throws — implementations must swallow and log their own failures so this can
/// never fail or delay the caller's job creation.
/// </summary>
public interface IJobListingCaptureService
{
    Task CaptureAsync(Job job, CancellationToken cancellationToken = default);
}
