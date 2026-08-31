using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;


namespace CvTailr.Web.Services;

// The one place HTTP calls to CvTailr.Api happen (see CvTailr.Web/CLAUDE.md).
public class ApiClient(HttpClient httpClient)
{
    // Matches CvTailr.Api's wire format: camelCase property names, enums as strings
    // (Api's Program.cs registers JsonStringEnumConverter on its HTTP JSON options).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<JdRequirements> ParseJdAsync(string jdText, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/jd/parse", new { jdText }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/jd/parse failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<JdRequirements>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/jd/parse returned an empty response body.");
    }

    public async Task<CvDocument> ParseCvAsync(string rawLatexSource, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/cv/parse", new { rawLatexSource }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new ApiClientException(
                $"POST /api/cv/parse failed with status {(int)response.StatusCode} {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CvDocument>(JsonOptions, ct);
        return result ?? throw new ApiClientException("POST /api/cv/parse returned an empty response body.");
    }
}

public class ApiClientException(string message) : Exception(message);
