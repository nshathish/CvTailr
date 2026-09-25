using CvTailr.Shared.Jobs;
using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface IJobsApiClient
{
    Task<Job?> GetJobByIdAsync(string jobId, CancellationToken ct = default);

    Task<JobCvResponse?> GetJobCvAsync(string jobId, CancellationToken ct = default);

    Task<List<JobSummary>> GetJobsAsync(CancellationToken ct = default);

    Task DeleteJobAsync(string jobId, CancellationToken ct = default);
}
