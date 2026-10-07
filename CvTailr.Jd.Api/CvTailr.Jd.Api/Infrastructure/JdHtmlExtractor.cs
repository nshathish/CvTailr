using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace CvTailr.Jd.Api.Infrastructure;

public class JdHtmlExtractor
{
    private const int MaxTextLength = 30_000;
    private static readonly string[] RemovedTags = ["script", "style", "nav", "header", "footer", "form", "aside"];

    public JdHtmlExtractionResult Extract(string html)
    {
        var document = new HtmlParser().ParseDocument(html);

        var jsonLdResult = TryExtractFromJsonLd(document);
        if (jsonLdResult is not null)
            return jsonLdResult with { Text = Truncate(jsonLdResult.Text) };

        return new JdHtmlExtractionResult(
            Truncate(ExtractFallbackText(document)),
            RoleTitle: null, CompanyName: null, Location: null, EmploymentType: null,
            DatePosted: null, ValidThrough: null, PostingUrl: null, HiringOrganizationUrls: []);
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

            return new JdHtmlExtractionResult(
                text,
                GetString(jobPosting, "title"),
                GetHiringOrganizationName(jobPosting),
                GetLocation(jobPosting),
                GetEmploymentType(jobPosting),
                GetDateTimeOffset(jobPosting, "datePosted"),
                GetDateTimeOffset(jobPosting, "validThrough"),
                GetString(jobPosting, "url"),
                GetHiringOrganizationUrls(jobPosting));
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

    private static List<string> GetHiringOrganizationUrls(JsonElement jobPosting)
    {
        if (!jobPosting.TryGetProperty("hiringOrganization", out var org) || org.ValueKind != JsonValueKind.Object)
            return [];

        var urls = new List<string>();

        var url = GetString(org, "url");
        if (!string.IsNullOrWhiteSpace(url))
            urls.Add(url);

        if (org.TryGetProperty("sameAs", out var sameAs))
        {
            switch (sameAs.ValueKind)
            {
                case JsonValueKind.String:
                    var value = sameAs.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        urls.Add(value);
                    break;

                case JsonValueKind.Array:
                    foreach (var entry in sameAs.EnumerateArray())
                    {
                        if (entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
                            urls.Add(entry.GetString()!);
                    }

                    break;
            }
        }

        return urls;
    }

    private static string? GetLocation(JsonElement jobPosting)
    {
        if (string.Equals(GetString(jobPosting, "jobLocationType"), "TELECOMMUTE", StringComparison.OrdinalIgnoreCase))
            return "Remote";

        if (!jobPosting.TryGetProperty("jobLocation", out var jobLocationElement))
            return null;

        var place = jobLocationElement.ValueKind == JsonValueKind.Array
            ? jobLocationElement.EnumerateArray().FirstOrDefault(p => p.ValueKind == JsonValueKind.Object)
            : jobLocationElement;

        if (place.ValueKind != JsonValueKind.Object ||
            !place.TryGetProperty("address", out var address) ||
            address.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var locality = GetString(address, "addressLocality");
        var country = GetAddressCountry(address);

        if (!string.IsNullOrWhiteSpace(locality) && !string.IsNullOrWhiteSpace(country))
            return $"{locality}, {country}";

        return !string.IsNullOrWhiteSpace(locality) ? locality : country;
    }

    private static string? GetAddressCountry(JsonElement address)
    {
        if (!address.TryGetProperty("addressCountry", out var country))
            return null;

        return country.ValueKind switch
        {
            JsonValueKind.String => country.GetString(),
            JsonValueKind.Object => GetString(country, "name"),
            _ => null
        };
    }

    private static string? GetEmploymentType(JsonElement jobPosting)
    {
        if (!jobPosting.TryGetProperty("employmentType", out var employmentType))
            return null;

        return employmentType.ValueKind switch
        {
            JsonValueKind.String => employmentType.GetString(),
            JsonValueKind.Array => employmentType.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement jobPosting, string propertyName)
    {
        var value = GetString(jobPosting, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
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

/// <summary>
/// RoleTitle/CompanyName are populated only when found in structured (JSON-LD) data — they take
/// priority over whatever the LLM later infers from Text. Location/EmploymentType/DatePosted/
/// ValidThrough/PostingUrl are likewise JSON-LD-only (null on the plain-text fallback path) and
/// exist solely for CvTailr.Api's JobListing capture (task 022) — they are never sent to the Web
/// client. HiringOrganizationUrls (hiringOrganization.url, then each sameAs entry, in order; empty
/// when absent) feeds CompanyDomain resolution (task 024) and is likewise server-side only.
/// </summary>
public record JdHtmlExtractionResult(
    string Text,
    string? RoleTitle,
    string? CompanyName,
    string? Location,
    string? EmploymentType,
    DateTimeOffset? DatePosted,
    DateTimeOffset? ValidThrough,
    string? PostingUrl,
    List<string> HiringOrganizationUrls);
