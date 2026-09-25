using System.Text.Json;
using CvTailr.Api.Exceptions;
using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

/// <summary>
/// Detects whether a JD-parse submission is raw text or a job posting URL, and for a URL,
/// fetches its content via Firecrawl's scrape API. No Firecrawl-specific concepts (request/
/// response shapes, endpoint paths) are exposed outside this class.
/// </summary>
public class FirecrawlJdSourceResolver(HttpClient httpClient, ILogger<FirecrawlJdSourceResolver> logger)
    : IJdSourceResolver
{
    private const int MinContentLength = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> ResolveAsync(string input, CancellationToken cancellationToken = default)
    {
        if (!TryParseHttpUrl(input, out var uri))
            return input;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(
                "v2/scrape",
                new ScrapeRequest(uri.ToString(), ["markdown"], true),
                JsonOptions,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Firecrawl scrape request failed for {Url}.", uri);
            throw FetchFailed(ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Firecrawl scrape for {Url} returned non-success status {StatusCode}.", uri, response.StatusCode);
            throw FetchFailed();
        }

        ScrapeResponse? scrapeResponse;
        try
        {
            scrapeResponse = await response.Content.ReadFromJsonAsync<ScrapeResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Firecrawl scrape response for {Url} was not valid JSON.", uri);
            throw FetchFailed(ex);
        }

        var markdown = scrapeResponse?.Data?.Markdown?.Trim();
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length < MinContentLength)
        {
            logger.LogWarning("Firecrawl scrape for {Url} returned empty/near-empty content.", uri);
            throw FetchFailed();
        }

        return markdown;
    }

    private static bool TryParseHttpUrl(string input, out Uri uri)
    {
        var parsed = Uri.TryCreate(input, UriKind.Absolute, out var candidate)
                     && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps);

        uri = parsed ? candidate! : null!;
        return parsed;
    }

    private static JdSourceResolutionException FetchFailed(Exception? innerException = null) =>
        new("Couldn't fetch that URL — try pasting the job description text instead.", innerException);

    protected sealed record ScrapeRequest(string Url, string[] Formats, bool OnlyMainContent);

    protected sealed class ScrapeResponse
    {
        public bool Success { get; init; }
        public ScrapeData? Data { get; init; }
    }

    protected sealed class ScrapeData
    {
        public string? Markdown { get; init; }
    }
}
