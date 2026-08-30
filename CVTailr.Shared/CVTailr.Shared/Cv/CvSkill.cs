using CvTailr.Shared.Enums;

namespace CvTailr.Shared.Cv;

public class CvSkill
{
    public string Name { get; set; } = string.Empty;
    public ProficiencyLevel Proficiency { get; set; }
    public bool IsProvisional { get; set; }
}
