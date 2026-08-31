using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using CvTailr.Shared.Tailoring;

namespace CvTailr.Api.Services.Interfaces;

public interface ITailoringService
{
    Task<TailoringProposal> ProposeAsync(
        CvDocument cvDocument,
        JdRequirements jdRequirements,
        MatchScoreResult? scoreResult,
        CancellationToken cancellationToken = default);

    TailorApplyResult ApplyAsync(
        CvDocument cvDocument,
        List<string> approvedBulletRewriteIds,
        List<BulletRewriteProposal> approvedRewrites,
        List<NewBulletProposal> approvedNewBullets,
        List<NewSkillProposal> approvedNewSkills);
}

/// <summary>
/// ApplyAsync's result: the updated document plus any warnings about approved items that
/// referenced a RoleId/BulletId no longer present in the given CvDocument (skipped, not applied).
/// </summary>
public record TailorApplyResult(CvDocument CvDocument, List<string> Warnings);
