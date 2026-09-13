namespace CvTailr.Api.Configuration;

public class FirecrawlOptions
{
    public const string SectionName = "Firecrawl";

    public string BaseUrl { get; set; } = "https://api.firecrawl.dev";

    public string ApiKey { get; set; } = string.Empty;
}
