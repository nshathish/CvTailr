namespace CvTailr.Web.Configuration;

public class LogoDevOptions
{
    public const string SectionName = "LogoDev";

    /// <summary>
    /// Logo.dev's publishable key — safe to use client-side (it's embedded directly in the
    /// &lt;img&gt; src on the rendered page), unlike a secret API key. Empty until set via user
    /// secrets locally / the LogoDev__PublishableKey App Service setting in Azure.
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;
}
