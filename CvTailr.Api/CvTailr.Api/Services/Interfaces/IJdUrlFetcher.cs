namespace CvTailr.Api.Services.Interfaces;

/// <summary>
/// Safely fetches the HTML of a job posting URL: validates the URL, protects against SSRF,
/// follows a bounded number of redirects, and caps how much of the response body is read.
/// Throws <see cref="CvTailr.Api.Exceptions.JdUrlException"/> ("UrlNotAllowed" or
/// "UrlFetchFailed") on failure.
/// </summary>
public interface IJdUrlFetcher
{
    Task<JdUrlFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default);
}

public record JdUrlFetchResult(string Html, string FinalUrl);
