namespace CvTailr.Api.Common.SkillTagging;

/// <summary>
/// Canonicalises free-text skill tags so equivalent phrasings compare equal between a user's
/// CvDocument.SkillTags and a JobListingRequirement.Tags (task 026). Pure and unit-testable —
/// used by SkillTaggingService and JobListingCaptureService.
/// </summary>
public static class TagCanonicalizer
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["k8s"] = "kubernetes",
        ["golang"] = "go",
        ["postgres"] = "postgresql",
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["c sharp"] = "c#",
        ["dotnet"] = ".net",
        [".net core"] = ".net",
        ["asp.net core"] = ".net",
        ["event driven architecture"] = "event-driven architecture",
        ["ci cd"] = "ci/cd"
    };

    /// <summary>Lowercase, trim, collapse whitespace, strip trailing punctuation, then alias. Empty for blank input.</summary>
    public static string Canonicalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var normalized = CollapseWhitespace(tag.ToLowerInvariant().Trim());
        normalized = normalized.TrimEnd('.', ',', ';', ':', '!', '?');

        return Aliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    /// <summary>Canonicalises every tag and returns the distinct, order-preserved, non-blank results.</summary>
    public static List<string> CanonicalizeAll(IEnumerable<string> tags) =>
        tags.Select(Canonicalize)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string CollapseWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
