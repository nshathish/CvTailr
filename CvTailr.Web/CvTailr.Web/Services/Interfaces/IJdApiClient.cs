using CvTailr.Shared.Jobs;

namespace CvTailr.Web.Services.Interfaces;

public interface IJdApiClient
{
    Task<Job> ParseJdAsync(string jdText, string? roleTitle, string? companyName, CancellationToken ct = default);
}
