using CvTailr.Shared.Jd;
using CvTailr.Shared.Jobs;

namespace CvTailr.Jd.Api.Application;

public record JdParseResult(JdRequirements JdRequirements, string? SourceUrl, JobSourceMetadata? SourceMetadata);