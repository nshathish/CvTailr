using CvTailr.Shared.Drill;
using CvTailr.Shared.Jd;

namespace CvTailr.Api.Services.Interfaces;

public interface IDrillService
{
    /// <summary>
    /// Resolves this job's TailoredCvDocument internally (falling back to the master CvDocument
    /// if none exists yet, same fallback as GET /api/jobs/{jobId}/cv) and weights candidate
    /// questions using ONLY this job's ledger entries — other jobs' provisional content is never
    /// considered. Throws KeyNotFoundException if the user has no CV at all.
    /// </summary>
    Task<DrillQuestion> GenerateQuestionAsync(
        string userId,
        string jobId,
        JdRequirements jdRequirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the outcome to the ledger via ILedgerService when question.RelatedLedgerEntryId is
    /// set; does nothing ledger-related otherwise (e.g. a gap-probe or general-behavioral question).
    /// </summary>
    Task<DrillAnswerEvaluation> EvaluateAnswerAsync(
        DrillQuestion question,
        string answerText,
        CancellationToken cancellationToken = default);
}
