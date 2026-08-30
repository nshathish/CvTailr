using CvTailr.Shared.Jd;

namespace CvTailr.Api.Services;

public interface IJdParsingService
{
    Task<JdRequirements> ParseAsync(string rawJdText, CancellationToken cancellationToken = default);
}
