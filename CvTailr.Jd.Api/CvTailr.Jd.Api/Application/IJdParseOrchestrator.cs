namespace CvTailr.Jd.Api.Application;

public interface IJdParseOrchestrator
{
    Task<JdParseResult> ParseAsync(string? jdText, string? jdUrl, CancellationToken cancellationToken = default);
}