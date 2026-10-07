using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CvTailr.Api.Features.Jobs;

public class JdServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor) : IJdServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<JdServiceParseOutcome> ParseAsync(
        string? jdText, string? jdUrl, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/jd/parse");
        request.Content = JsonContent.Create(new { jdText, jdUrl }, options: JsonOptions);

        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<JdServiceParseError>(JsonOptions, cancellationToken);
            return new JdServiceParseOutcome(Result: null, Error: error);
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JdServiceParseResult>(JsonOptions, cancellationToken);
        return new JdServiceParseOutcome(
            Result: result ?? throw new InvalidOperationException("CvTailr.Jd.Api returned an empty parse response."),
            Error: null);
    }
}