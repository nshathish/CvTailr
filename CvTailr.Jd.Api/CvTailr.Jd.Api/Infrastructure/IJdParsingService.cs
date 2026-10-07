using CvTailr.Shared.Jd;

namespace CvTailr.Jd.Api.Infrastructure;

public interface IJdParsingService
{
    Task<JdRequirements> ParseAsync(string rawJdText, CancellationToken cancellationToken = default);
}
