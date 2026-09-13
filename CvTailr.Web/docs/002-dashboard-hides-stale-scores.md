## Symptom

After replacing the master CV, dashboard cards still rendered the previous match percentage as if it were current.

## Diagnosis

Staleness was detected (`cv.UpdatedAt > job.MatchScoreResult.ScoredAt`) and shown as a badge, but the score ring and matched-skills summary still used the stale `MatchScoreResult` values.

## Fix

Updated dashboard card rendering so stale scores are no longer shown as current:
- show score ring/skills summary only when the score is not stale,
- show explicit stale text (`Needs re-score` / `Score outdated`) when staleness is detected.

## Verification

`dotnet build /home/runner/work/CvTailr/CvTailr/CvTailr.Web/CvTailr.Web.slnx`
