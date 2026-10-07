using CvTailr.Shared.Enums;
using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Features.Jobs.Listings;

/// <summary>
/// Pure match-percentage calculation between a JobListing's requirements and a user's
/// CvDocument.SkillTags (task 026). Unit-testable, no I/O.
/// </summary>
public static class JobListingMatcher
{
    private const int MustHaveWeight = 2;
    private const int NiceToHaveWeight = 1;

    public static JobListingMatchScore Calculate(
        IReadOnlyList<JobListingRequirement> requirements, IReadOnlyCollection<string> userSkillTags)
    {
        var tagSet = new HashSet<string>(userSkillTags, StringComparer.OrdinalIgnoreCase);

        var coveredMustHaves = new List<string>();
        var coveredNiceToHaves = new List<string>();
        var totalWeight = 0;
        var coveredWeight = 0;

        foreach (var requirement in requirements)
        {
            var weight = requirement.Priority == RequirementPriority.MustHave ? MustHaveWeight : NiceToHaveWeight;
            totalWeight += weight;

            if (!requirement.Tags.Any(tag => tagSet.Contains(tag)))
                continue;

            coveredWeight += weight;
            (requirement.Priority == RequirementPriority.MustHave ? coveredMustHaves : coveredNiceToHaves)
                .Add(requirement.Skill);
        }

        var matchPercent = totalWeight == 0 ? 0 : (int)Math.Round(100.0 * coveredWeight / totalWeight);

        return new JobListingMatchScore(matchPercent, coveredMustHaves.Concat(coveredNiceToHaves).ToList());
    }
}

/// <summary>MatchedSkills lists must-have skills before nice-to-have ones.</summary>
public record JobListingMatchScore(int MatchPercent, List<string> MatchedSkills);
