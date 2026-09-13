using CvTailr.Api.Data.Interfaces;
using CvTailr.Shared.Cv;

namespace CvTailr.Api.Helpers.Extensions;

/// <summary>
/// Shared "which CvDocument should this job's tailoring/scoring use" resolution — a job's own
/// TailoredCvDocument if one already exists, otherwise the user's master CvDocument.
/// </summary>
public static class CvDocumentResolutionExtensions
{
    public static async Task<CvDocument?> ResolveBaseDocumentAsync(
        this ITailoredCvRepository tailoredCvRepository,
        ICvRepository cvRepository,
        string userId,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var tailoredCv = await tailoredCvRepository.GetByJobIdAsync(jobId, cancellationToken);
        if (tailoredCv is not null)
            return tailoredCv.ToCvDocument();

        return await cvRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>
    /// TailoredCvDocument and CvDocument share the same Roles/Skills shape, so callers that operate
    /// on "a document" generically can work against either without duplicating any logic — this
    /// just adapts the shape.
    /// </summary>
    public static CvDocument ToCvDocument(this TailoredCvDocument tailoredCvDocument) => new()
    {
        Id = tailoredCvDocument.CvId,
        UserId = tailoredCvDocument.UserId,
        Roles = tailoredCvDocument.Roles,
        Skills = tailoredCvDocument.Skills
    };
}
