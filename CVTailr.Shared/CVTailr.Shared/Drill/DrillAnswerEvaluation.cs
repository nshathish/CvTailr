using CvTailr.Shared.Ledger;

namespace CvTailr.Shared.Drill;

public class DrillAnswerEvaluation
{
    public string DrillQuestionId { get; set; } = string.Empty;
    public DrillOutcome Outcome { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string? SuggestedFollowUp { get; set; }
}
