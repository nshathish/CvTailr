using CvTailr.Shared.Jobs;
using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface IJobsApiClient
{
    /// <summary>Exactly one of jdText/jdUrl must be non-empty.</summary>
    Task<JdParseOutcome> ParseJdAsync(string? jdText, string? jdUrl, CancellationToken ct = default);

    Task<Job?> GetJobByIdAsync(string jobId, CancellationToken ct = default);

    /// <summary>Saves the job title/company/website confirmed in the New Job wizard's review step.</summary>
    Task<Job> UpdateJobDetailsAsync(
        string jobId, string roleTitle, string companyName, string? companyDomain, CancellationToken ct = default);

    Task<JobCvResponse?> GetJobCvAsync(string jobId, CancellationToken ct = default);

    Task<List<JobSummary>> GetJobsAsync(CancellationToken ct = default);

    Task DeleteJobAsync(string jobId, CancellationToken ct = default);
}
