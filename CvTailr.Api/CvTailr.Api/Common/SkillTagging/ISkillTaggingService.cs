using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;

namespace CvTailr.Api.Common.SkillTagging;

/// <summary>
/// Tags job requirements and CV content with short, common skill/technology/practice names (task
/// 026), for matching a user's CV against shared job listings. Every tag returned has already
/// been run through TagCanonicalizer.
/// </summary>
public interface ISkillTaggingService
{
    /// <summary>1-3 tags per requirement, in the same order as the input.</summary>
    Task<List<List<string>>> TagRequirementsAsync(
        List<JdRequirement> requirements, CancellationToken cancellationToken = default);

    /// <summary>Up to 60 tags for the skills evidenced anywhere in the CV.</summary>
    Task<List<string>> TagCvAsync(CvDocument cv, CancellationToken cancellationToken = default);
}
