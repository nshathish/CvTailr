namespace CvTailr.Shared.Cv;

public class CvBullet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalText { get; set; } = string.Empty;
    public string? TailoredText { get; set; }
    public string? Language { get; set; }
    public bool IsProvisional { get; set; }
}
