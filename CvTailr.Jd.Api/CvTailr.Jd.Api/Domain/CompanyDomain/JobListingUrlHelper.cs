using System.Net;

namespace CvTailr.Jd.Api.Domain.CompanyDomain;

/// <summary>
/// Pure URL string helpers for <see cref="JobListingCaptureService"/>. Both Canonicalise and
/// CleanApplyUrl take the final URL after redirects (Job.SourceUrl from task 020).
/// </summary>
public static class JobListingUrlHelper
{
    private static readonly HashSet<string> CanonicalExactDrops =
        new(StringComparer.OrdinalIgnoreCase) { "gclid", "fbclid", "msclkid", "ref", "source", "trk", "src" };

    private static readonly HashSet<string> ApplyUrlExactDrops =
        new(StringComparer.OrdinalIgnoreCase) { "gclid", "fbclid", "msclkid" };

    private static readonly HashSet<string> ThreeLabelSecondLevelSuffixes =
        new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "gov", "ac", "net" };

    /// <summary>
    /// A de-duplication key, never shown to users: lowercases scheme/host, drops the default port
    /// and fragment, strips tracking query parameters, sorts the rest by name, and removes a
    /// trailing slash from the path (except the root "/").
    /// </summary>
    public static string Canonicalise(string url)
    {
        var uri = new Uri(url);

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var portSegment = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        var path = uri.AbsolutePath;
        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        var pairs = ParseQueryPairs(uri.Query)
            .Where(p => !IsDropped(p.Key, CanonicalExactDrops))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        var queryString = pairs.Count == 0
            ? string.Empty
            : "?" + string.Join('&', pairs.Select(p => p.Value is null
                ? Uri.EscapeDataString(p.Key)
                : $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{scheme}://{host}{portSegment}{path}{queryString}";
    }

    /// <summary>
    /// The user-facing ApplyUrl, cleaned only of the given tracking parameters. Everything else —
    /// parameter order, path/query case, fragment — is preserved exactly as given, so this works on
    /// the raw string rather than reconstructing the URL from its parsed parts.
    /// </summary>
    public static string CleanApplyUrl(string url)
    {
        var fragmentIndex = url.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var withoutFragment = fragmentIndex >= 0 ? url[..fragmentIndex] : url;

        var queryIndex = withoutFragment.IndexOf('?');
        if (queryIndex < 0)
            return url;

        var beforeQuery = withoutFragment[..queryIndex];
        var rawQuery = withoutFragment[(queryIndex + 1)..];
        if (rawQuery.Length == 0)
            return url;

        var keptPairs = rawQuery
            .Split('&')
            .Where(pair =>
            {
                var eqIndex = pair.IndexOf('=');
                var rawKey = eqIndex >= 0 ? pair[..eqIndex] : pair;
                var decodedKey = Uri.UnescapeDataString(rawKey);
                return !IsDropped(decodedKey, ApplyUrlExactDrops);
            })
            .ToList();

        var queryString = keptPairs.Count == 0 ? string.Empty : "?" + string.Join('&', keptPairs);
        return beforeQuery + queryString + fragment;
    }

    /// <summary>
    /// Compares the registrable domain of two hosts: the last two labels, or three when the
    /// second-last label is one of co/com/org/gov/ac/net and the last is a two-letter country code.
    /// Never true when either side is invalid/IP/localhost, even against itself (both null).
    /// </summary>
    public static bool IsSameRegistrableDomain(string hostA, string hostB)
    {
        var domainA = GetRegistrableDomain(hostA);
        var domainB = GetRegistrableDomain(hostB);
        return domainA is not null && string.Equals(domainA, domainB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The registrable domain (e.g. "monzo.com") of a URL, host, or bare domain: lowercases,
    /// strips scheme/path/query/port and a leading "www.", then keeps the last two labels (three
    /// for co.uk-style suffixes — see <see cref="IsSameRegistrableDomain"/>). Null for invalid
    /// input, IP addresses, and localhost.
    /// </summary>
    public static string? GetRegistrableDomain(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var host = ExtractHost(input.Trim())?.ToLowerInvariant();
        if (string.IsNullOrEmpty(host))
            return null;

        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        if (host.Length == 0 ||
            string.Equals(host, "localhost", StringComparison.Ordinal) ||
            IPAddress.TryParse(host, out _))
        {
            return null;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 2)
            return host;

        var last = labels[^1];
        var secondLast = labels[^2];
        var takeCount = last.Length == 2 && ThreeLabelSecondLevelSuffixes.Contains(secondLast) ? 3 : 2;
        takeCount = Math.Min(takeCount, labels.Length);

        return string.Join('.', labels.Skip(labels.Length - takeCount));
    }

    /// <summary>
    /// Resolves the host from either an absolute URL or a bare host/domain (which has no scheme,
    /// so it isn't itself a valid absolute URI) by re-parsing the latter with a dummy "http://"
    /// prefix.
    /// </summary>
    private static string? ExtractHost(string input)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out var absolute) && !string.IsNullOrEmpty(absolute.Host))
            return absolute.Host;

        return Uri.TryCreate("http://" + input, UriKind.Absolute, out var withScheme) && !string.IsNullOrEmpty(withScheme.Host)
            ? withScheme.Host
            : null;
    }

    private static bool IsDropped(string paramName, IReadOnlySet<string> exactDrops) =>
        paramName.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || exactDrops.Contains(paramName);

    private static List<KeyValuePair<string, string?>> ParseQueryPairs(string query)
    {
        var result = new List<KeyValuePair<string, string?>>();
        if (string.IsNullOrEmpty(query) || query == "?")
            return result;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eqIndex >= 0 ? pair[..eqIndex] : pair);
            var value = eqIndex >= 0 ? Uri.UnescapeDataString(pair[(eqIndex + 1)..]) : null;
            result.Add(new(key, value));
        }

        return result;
    }
}
