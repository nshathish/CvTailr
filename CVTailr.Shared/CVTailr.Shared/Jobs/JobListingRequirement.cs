using CvTailr.Shared.Enums;

namespace CvTailr.Shared.Jobs;

/// <summary>
/// A JobListing's copy of a JdRequirement, plus the skill tags it was tagged with (task 026) for
/// matching against a user's CvDocument.SkillTags. Skill/Priority/YearsRequired/Notes mirror
/// JdRequirement exactly.
/// </summary>
public class JobListingRequirement
{
    public string Skill { get; set; } = string.Empty;
    public RequirementPriority Priority { get; set; }
    public string? YearsRequired { get; set; }
    public string? Notes { get; set; }

    /// <summary>1-4 word common skill/technology/practice names, canonicalised (TagCanonicalizer).</summary>
    public List<string> Tags { get; set; } = [];
}
