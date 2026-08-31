using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;

namespace CvTailr.Api.Services.Interfaces;

public interface IScoringService
{
    Task<MatchScoreResult> ScoreAsync(
        JdRequirements jdRequirements,
        CvDocument cvDocument,
        CancellationToken cancellationToken = default);
}
