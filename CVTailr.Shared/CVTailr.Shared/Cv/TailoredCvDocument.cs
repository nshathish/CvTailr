namespace CvTailr.Shared.Cv;

/// <summary>
/// A full, independent snapshot of a candidate's CV tailored for one specific Job — never a
/// diff/patch against the master CvDocument. One per JobId; the master CvDocument itself is
/// never mutated by tailoring (see root CLAUDE.md).
/// </summary>
public class TailoredCvDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;

    /// <summary>The master CvDocument's Id this snapshot was derived from.</summary>
    public string CvId { get; set; } = string.Empty;

    /// <summary>Partition key for persistence — one TailoredCvDocument per JobId.</summary>
    public string JobId { get; set; } = string.Empty;

    public List<CvRole> Roles { get; set; } = new();
    public List<CvSkill> Skills { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
