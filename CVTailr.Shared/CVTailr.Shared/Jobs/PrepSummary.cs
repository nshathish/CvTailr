namespace CvTailr.Shared.Jobs;

/// <summary>Snapshot of a job's interview-prep readiness, surfaced on job responses.</summary>
public record PrepSummary(int Total, int Ready, int AssessedPassed);
