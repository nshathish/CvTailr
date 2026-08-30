namespace CvTailr.Shared.Cv;

public class CvRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public List<CvBullet> Bullets { get; set; } = new();
}
