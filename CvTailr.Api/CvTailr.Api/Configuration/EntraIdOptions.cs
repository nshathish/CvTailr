namespace CvTailr.Api.Configuration;

public class EntraIdOptions
{
    public const string SectionName = "EntraId";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;
}
