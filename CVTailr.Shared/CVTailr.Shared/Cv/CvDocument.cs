namespace CvTailr.Shared.Cv;

public class CvDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Partition key for persistence — one CvDocument per UserId.</summary>
    public string UserId { get; set; } = string.Empty;

    public List<CvRole> Roles { get; set; } = new();
    public List<CvSkill> Skills { get; set; } = new();
    public string? RawLatexSource { get; set; }
}
