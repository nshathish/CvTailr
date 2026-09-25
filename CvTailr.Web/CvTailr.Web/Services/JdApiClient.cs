using CvTailr.Shared.Jobs;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class JdApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), IJdApiClient
{
    public async Task<Job> ParseJdAsync(string jdText, string? roleTitle, string? companyName, CancellationToken ct = default)
    {
        const string relativePath = "api/jd/parse";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { jdText, roleTitle, companyName }, options: JsonOptions),
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<Job>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body.");
    }
}
