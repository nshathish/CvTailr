using CvTailr.Shared.Jd;

namespace CvTailr.Api.Features.Jd;

public interface IJdParsingService
{
    Task<JdRequirements> ParseAsync(string rawJdText, CancellationToken cancellationToken = default);
}
