namespace CvTailr.Shared.Cv;

public class CvDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Partition key for persistence — one CvDocument per UserId.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public List<CvRole> Roles { get; set; } = [];
    public List<CvSkill> Skills { get; set; } = [];
    public string? RawSourceText { get; set; }

    /// <summary>
    ///     Set fresh every time this document is (re-)parsed and replaces the prior one. CvId
    ///     itself never changes across a re-upload (the same CvDocument.Id is preserved in place),
    ///     so this is the only signal for "has the master CV changed since X" — e.g. comparing
    ///     against MatchScoreResult.ScoredAt or TailoredCvDocument.CreatedAt.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}