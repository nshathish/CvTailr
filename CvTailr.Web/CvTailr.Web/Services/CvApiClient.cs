using System.Net;
using CvTailr.Shared.Cv;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class CvApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), ICvApiClient
{
    public async Task<CvDocument> ParseCvAsync(string rawLatexSource, CancellationToken ct = default)
    {
        const string relativePath = "api/cv/parse";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { rawLatexSource }, options: JsonOptions),
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<CvDocument>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body.");
    }

    public async Task<CvDocument?> GetCurrentCvAsync(CancellationToken ct = default)
    {
        const string relativePath = "api/cv/current";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "GET";
                options.RelativePath = relativePath;
            },
            cancellationToken: ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfUnsuccessfulAsync(response, "GET", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<CvDocument>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"GET /{relativePath} returned an empty response body.");
    }
}
