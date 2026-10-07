using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Features.Jobs;

public interface IJdServiceClient
{
    /// <summary>Exactly one of jdText/jdUrl must be non-empty.</summary>
    Task<JdServiceParseOutcome> ParseAsync(string? jdText, string? jdUrl, CancellationToken cancellationToken = default);
}

/// <summary>Exactly one of Result/Error is populated.</summary>
public record JdServiceParseOutcome(JdServiceParseResult? Result, JdServiceParseError? Error);

/// <summary>Mirrors CvTailr.Jd.Api's JdParseResult wire shape.</summary>
public record JdServiceParseResult(JdRequirements JdRequirements, string? SourceUrl, JobSourceMetadata? SourceMetadata);

/// <summary>Mirrors CvTailr.Jd.Api's JdParseError wire shape (422 response body).</summary>
public record JdServiceParseError(string Code, string Message);
