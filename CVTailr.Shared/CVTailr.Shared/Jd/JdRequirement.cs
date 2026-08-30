using CvTailr.Shared.Enums;

namespace CvTailr.Shared.Jd;

public class JdRequirement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Skill { get; set; } = string.Empty;
    public RequirementPriority Priority { get; set; }
    public string? YearsRequired { get; set; }
    public string? Notes { get; set; }
}
