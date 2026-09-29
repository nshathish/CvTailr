using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

public class JdHtmlExtractor : IJdHtmlExtractor
{
    private const int MaxTextLength = 30_000;
    private static readonly string[] RemovedTags = ["script", "style", "nav", "header", "footer", "form", "aside"];

    public JdHtmlExtractionResult Extract(string html)
    {
        var document = new HtmlParser().ParseDocument(html);

        var jsonLdResult = TryExtractFromJsonLd(document);
        if (jsonLdResult is not null)
            return jsonLdResult with { Text = Truncate(jsonLdResult.Text) };

        return new JdHtmlExtractionResult(Truncate(ExtractFallbackText(document)), null, null);
    }

    private static JdHtmlExtractionResult? TryExtractFromJsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var json = script.TextContent;
            if (string.IsNullOrWhiteSpace(json))
                continue;

            JsonElement root;
            try
            {
                using var parsed = JsonDocument.Parse(json);
                root = parsed.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            if (!TryFindJobPosting(root, out var jobPosting))
                continue;

            var text = ExtractDescriptionText(jobPosting);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            return new JdHtmlExtractionResult(text, GetString(jobPosting, "title"), GetHiringOrganizationName(jobPosting));
        }

        return null;
    }

    private static bool TryFindJobPosting(JsonElement element, out JsonElement jobPosting)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsJobPosting(element))
                {
                    jobPosting = element;
                    return true;
                }

                if (element.TryGetProperty("@graph", out var graph) && TryFindJobPosting(graph, out jobPosting))
                    return true;

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindJobPosting(item, out jobPosting))
                        return true;
                }

                break;
        }

        jobPosting = default;
        return false;
    }

    private static bool IsJobPosting(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
            return false;

        return type.ValueKind switch
        {
            JsonValueKind.String => string.Equals(type.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => type.EnumerateArray().Any(t =>
                t.ValueKind == JsonValueKind.String &&
                string.Equals(t.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static string? ExtractDescriptionText(JsonElement jobPosting)
    {
        var description = GetString(jobPosting, "description");
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var fragment = new HtmlParser().ParseDocument($"<body>{description}</body>");
        return CollapseWhitespace(fragment.Body?.TextContent ?? description);
    }

    private static string? GetHiringOrganizationName(JsonElement jobPosting)
    {
        if (!jobPosting.TryGetProperty("hiringOrganization", out var org))
            return null;

        return org.ValueKind switch
        {
            JsonValueKind.Object => GetString(org, "name"),
            JsonValueKind.String => org.GetString(),
            JsonValueKind.Array => org.EnumerateArray()
                .Where(o => o.ValueKind == JsonValueKind.Object)
                .Select(o => GetString(o, "name"))
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
            _ => null
        };
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ExtractFallbackText(IDocument document)
    {
        foreach (var tag in RemovedTags)
        {
            foreach (var element in document.QuerySelectorAll(tag).ToList())
                element.Remove();
        }

        var content = document.QuerySelector("main") ?? document.QuerySelector("article") ?? document.Body;
        return CollapseWhitespace(content?.TextContent ?? string.Empty);
    }

    private static string CollapseWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string text) =>
        text.Length <= MaxTextLength ? text : text[..MaxTextLength];
}
