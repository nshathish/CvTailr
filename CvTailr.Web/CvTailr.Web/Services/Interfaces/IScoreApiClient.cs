using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface IScoreApiClient
{
    Task<ScoreOutcome> GetScoreAsync(string jobId, CancellationToken ct = default);
}
