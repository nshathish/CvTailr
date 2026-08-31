using CvTailr.Shared.Cv;
using CvTailr.Shared.Drill;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Services.Interfaces;

public interface IDrillService
{
    Task<DrillQuestion> GenerateQuestionAsync(
        CvDocument cvDocument,
        JdRequirements jdRequirements,
        List<LedgerEntry> ledgerEntries,
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
