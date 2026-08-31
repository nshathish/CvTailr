namespace CvTailr.Api.Configuration;

public class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Omit to authenticate with DefaultAzureCredential instead of an account key.</summary>
    public string? AccountKey { get; set; }

    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>Ledger entries container, partition key /cvId.</summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>Persisted CvDocuments container, partition key /userId.</summary>
    public string CvContainerName { get; set; } = string.Empty;
}
