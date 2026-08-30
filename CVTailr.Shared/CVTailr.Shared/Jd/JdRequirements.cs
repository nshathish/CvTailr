namespace CvTailr.Shared.Jd;

public class JdRequirements
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RoleTitle { get; set; } = string.Empty;
    public string RawJdText { get; set; } = string.Empty;
    public List<JdRequirement> Requirements { get; set; } = new();
    public List<string> EmphasizedLanguages { get; set; } = new();
}
