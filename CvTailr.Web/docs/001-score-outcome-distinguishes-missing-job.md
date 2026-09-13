## Symptom

Scoring treated every `/api/score` 404 as "No CV found", so an invalid or deleted job could surface the wrong UI state.

## Diagnosis

The score API returned 404 for both "job not found" and "CV missing", and the Web client collapsed both responses into `ScoreOutcome.CvMissing`.

## Fix

Changed `/api/score` to return 409 Conflict when the current user has no uploaded CV, kept 404 for an unknown job, and updated `ScoreApiClient` plus its callers to branch on separate `CvMissing` and `JobMissing` outcomes.

## Verification

`dotnet build /home/runner/work/CvTailr/CvTailr/CvTailr.Api/CvTailr.Api.slnx`

`dotnet build /home/runner/work/CvTailr/CvTailr/CvTailr.Web/CvTailr.Web.slnx`
