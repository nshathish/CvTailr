namespace CvTailr.Shared.Tailoring;

public class TailoringProposal
{
    public string CvId { get; set; } = string.Empty;
    public string JdId { get; set; } = string.Empty;
    public List<BulletRewriteProposal> BulletRewrites { get; set; } = new();
    public List<NewBulletProposal> NewBullets { get; set; } = new();
    public List<NewSkillProposal> NewSkills { get; set; } = new();
}
