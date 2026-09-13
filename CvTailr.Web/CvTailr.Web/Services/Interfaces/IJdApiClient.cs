using CvTailr.Shared.Jobs;

namespace CvTailr.Web.Services.Interfaces;

public interface IJdApiClient
{
    Task<Job> ParseJdAsync(string jdText, CancellationToken ct = default);
}
