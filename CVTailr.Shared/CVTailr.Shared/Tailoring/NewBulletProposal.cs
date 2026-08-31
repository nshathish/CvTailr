namespace CvTailr.Shared.Tailoring;

public class NewBulletProposal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RoleId { get; set; } = string.Empty;
    public string ProposedText { get; set; } = string.Empty;
    public string? ProposedLanguage { get; set; }
    public string Reason { get; set; } = string.Empty;
}
