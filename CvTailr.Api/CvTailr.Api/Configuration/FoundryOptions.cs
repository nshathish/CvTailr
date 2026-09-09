namespace CvTailr.Api.Configuration;

public class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Omit to authenticate with DefaultAzureCredential instead of a key.</summary>
    public string? ApiKey { get; set; }

    public string CvParsingDeploymentName { get; set; } = string.Empty;

    public string JdParsingDeploymentName { get; set; } = string.Empty;

    public string ScoringDeploymentName { get; set; } = string.Empty;
}
