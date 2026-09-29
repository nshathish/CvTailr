namespace CvTailr.Shared.Jd;

public class JdRequirements
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? RoleTitle { get; set; }
    public string? CompanyName { get; set; }
    public string RawJdText { get; set; } = string.Empty;
    public List<JdRequirement> Requirements { get; set; } = [];
    public List<string> EmphasizedLanguages { get; set; } = [];
}
