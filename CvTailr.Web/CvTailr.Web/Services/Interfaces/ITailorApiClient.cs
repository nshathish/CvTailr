using CvTailr.Shared.Tailoring;
using CvTailr.Web.Services;

namespace CvTailr.Web.Services.Interfaces;

public interface ITailorApiClient
{
    Task<TailoringProposal> ProposeTailoringAsync(string jobId, CancellationToken ct = default);

    Task<TailorApplyResult> ApplyTailoringAsync(
        string jobId,
        List<string> approvedBulletRewriteIds,
        List<BulletRewriteProposal> approvedRewrites,
        List<NewBulletProposal> approvedNewBullets,
        List<NewSkillProposal> approvedNewSkills,
        CancellationToken ct = default);
}
