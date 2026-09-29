using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using CvTailr.Api.Exceptions;
using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

/// <summary>
/// Fetches a job posting URL's HTML for <see cref="JdSourceResolver"/>. SSRF protection lives in
/// the <see cref="ConnectCallback"/> registered on this class's <see cref="System.Net.Http.SocketsHttpHandler"/>
/// (see Program.cs) via <see cref="PrivateNetworkGuard"/> — every connection this HttpClient makes,
/// including across redirects, resolves the host itself and only connects to a validated public IP.
/// </summary>
public class JdUrlFetcher(HttpClient httpClient, ILogger<JdUrlFetcher> logger) : IJdUrlFetcher
{
    private const int MaxUrlLength = 2048;
    private const int MaxRedirects = 3;
    private const int MaxBodyBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(10);

    public async Task<JdUrlFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var currentUri = ValidateUrl(url);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(OverallTimeout);

        for (var redirectCount = 0; ; redirectCount++)
        {
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                if (FindUrlException(ex) is { } urlException)
                {
                    logger.LogWarning("JD URL fetch blocked for {Url}: {Reason}", currentUri, urlException.Message);
                    throw urlException;
                }

                logger.LogWarning(ex, "JD URL fetch failed for {Url}.", currentUri);
                throw UrlFetchFailed();
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode))
                {
                    if (redirectCount >= MaxRedirects || response.Headers.Location is null)
                    {
                        logger.LogWarning(
                            "JD URL fetch for {Url} exceeded {MaxRedirects} redirects or had no Location header.",
                            currentUri, MaxRedirects);
                        throw UrlFetchFailed();
                    }

                    currentUri = ValidateUrl(new Uri(currentUri, response.Headers.Location).ToString());
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("JD URL fetch for {Url} returned status {StatusCode}.", currentUri, response.StatusCode);
                    throw UrlFetchFailed();
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "JD URL fetch for {Url} returned non-HTML content type '{MediaType}'.", currentUri, mediaType);
                    throw UrlFetchFailed();
                }

                string html;
                try
                {
                    html = await ReadLimitedAsync(response, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
                {
                    logger.LogWarning(ex, "JD URL fetch failed while reading body for {Url}.", currentUri);
                    throw UrlFetchFailed();
                }

                logger.LogInformation("JD URL fetch succeeded for {FinalUrl}.", currentUri);
                return new JdUrlFetchResult(html, currentUri.ToString());
            }
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int bytesRead;
        while (buffer.Length < MaxBodyBytes &&
               (bytesRead = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            var remaining = MaxBodyBytes - buffer.Length;
            await buffer.WriteAsync(chunk.AsMemory(0, (int)Math.Min(bytesRead, remaining)), cancellationToken);
        }

        return ResolveEncoding(response.Content.Headers.ContentType).GetString(buffer.ToArray());
    }

    private static Encoding ResolveEncoding(MediaTypeHeaderValue? contentType)
    {
        var charset = contentType?.CharSet?.Trim('"');
        if (string.IsNullOrWhiteSpace(charset))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static Uri ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength)
            throw UrlNotAllowed();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw UrlNotAllowed();
        }

        return uri;
    }

    private static JdUrlException? FindUrlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is JdUrlException urlException)
                return urlException;
        }

        return null;
    }

    private static JdUrlException UrlNotAllowed() =>
        new("UrlNotAllowed", "That URL can't be fetched — try pasting the job description text instead.");

    private static JdUrlException UrlFetchFailed() =>
        new("UrlFetchFailed", "Couldn't fetch that URL — try pasting the job description text instead.");

    /// <summary>
    /// Registered as the <see cref="System.Net.Http.SocketsHttpHandler.ConnectCallback"/> for this
    /// client (see Program.cs). Resolves the host itself — bypassing HttpClient's own DNS-then-connect
    /// path — so every candidate address can be checked against <see cref="PrivateNetworkGuard"/>
    /// before a socket is ever opened, and connects only to the first address that passes.
    /// </summary>
    internal static async ValueTask<Stream> ConnectCallback(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        }
        catch (SocketException)
        {
            throw UrlFetchFailed();
        }

        var address = addresses.FirstOrDefault(PrivateNetworkGuard.IsPublicAddress);
        if (address is null)
            throw UrlNotAllowed();

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
