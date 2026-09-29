using CvTailr.Shared.Jd;

namespace CvTailr.Api.Services.Interfaces;

public interface IJdParsingService
{
    Task<JdRequirements> ParseAsync(string rawJdText, CancellationToken cancellationToken = default);
}
