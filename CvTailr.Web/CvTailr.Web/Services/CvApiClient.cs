using System.Net;
using CvTailr.Shared.Cv;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class CvApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), ICvApiClient
{
    public async Task<CvDocument> ParseCvAsync(
        Stream? fileStream, string? fileName, string? rawCvText, CancellationToken ct = default)
    {
        const string relativePath = "api/cv/parse";

        using var content = new MultipartFormDataContent();
        if (fileStream is not null)
        {
            content.Add(new StreamContent(fileStream), "cvFile", fileName ?? "cv");
        }

        if (!string.IsNullOrWhiteSpace(rawCvText))
        {
            content.Add(new StringContent(rawCvText), "rawCvText");
        }

        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: content,
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
