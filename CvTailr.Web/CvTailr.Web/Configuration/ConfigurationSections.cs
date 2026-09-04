namespace CvTailr.Web.Configuration;

// Names of top-level sections in appsettings.json, shared between Program.cs's
// configuration binding and any service that needs to reference the same section
// by name (e.g. IDownstreamApi's serviceName parameter).
public static class ConfigurationSections
{
    public const string AzureAd = "AzureAd";
    public const string DownstreamApi = "DownstreamApi";
}
