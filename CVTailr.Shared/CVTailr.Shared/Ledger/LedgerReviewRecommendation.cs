namespace CvTailr.Shared.Ledger;

public class LedgerReviewRecommendation
{
    public string LedgerEntryId { get; set; } = string.Empty;
    public RecommendedAction RecommendedAction { get; set; }
    public string Reason { get; set; } = string.Empty;
}
