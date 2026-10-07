namespace CvTailr.Api.Features.Jobs;

public class JdApiOptions
{
    public const string SectionName = "JdApi";

    /// <summary>Base address of the internal CvTailr.Jd.Api service, e.g. "https://localhost:7236".</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
