namespace CvTailr.Shared.Ledger;

public class DrillAttempt
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public DrillOutcome Outcome { get; set; }
    public string? Notes { get; set; }
}
