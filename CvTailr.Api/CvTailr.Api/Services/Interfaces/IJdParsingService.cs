using CvTailr.Shared.Jd;

namespace CvTailr.Api.Services.Interfaces;

public interface IJdParsingService
{
    Task<JdRequirements> ParseAsync(string rawJdText, string? roleTitle, string? companyName,
        CancellationToken cancellationToken = default);
}
