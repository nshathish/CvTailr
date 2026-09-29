using System.Net;
using CvTailr.Shared.Jobs;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class JdApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IJdApiClient
{
    public async Task<JdParseOutcome> ParseJdAsync(string? jdText, string? jdUrl, CancellationToken ct = default)
    {
        const string relativePath = "api/jd/parse";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { jdText, jdUrl }, options: JsonOptions),
            cancellationToken: ct);

        // The Api returns 422 with a specific error code for a URL it couldn't turn into JD text
        // (see CvTailr.Api's JdEndpoints/JdUrlException) — map the ones the page knows how to show
        // inline; anything else falls through to the generic error handling below.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<JdParseError>(JsonOptions, ct);
            var urlError = error?.Code switch
            {
                "UrlNotAllowed" => JdUrlErrorKind.NotAllowed,
                "UrlFetchFailed" => JdUrlErrorKind.FetchFailed,
                "UrlNotReadable" => JdUrlErrorKind.NotReadable,
                _ => (JdUrlErrorKind?)null
            };

            if (urlError is not null)
                return new JdParseOutcome(Job: null, UrlError: urlError);

            throw new ApiClientException(
                $"POST /{relativePath} failed with status 422 {response.StatusCode}: {error?.Message ?? "Unprocessable request."}");
        }

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return new JdParseOutcome(
            Job: result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body."),
            UrlError: null);
    }

    private record JdParseError(string Code, string Message);
}

public enum JdUrlErrorKind
{
    NotAllowed,
    FetchFailed,
    NotReadable
}

public record JdParseOutcome(Job? Job, JdUrlErrorKind? UrlError);
