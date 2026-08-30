\# CvTailr.Shared



Core domain models shared between `CvTailr.Api` and (only via HTTP/OpenAPI

contract, never direct reference) the UI-layer projects. This is a plain

.NET class library — no business logic, no I/O, no dependencies on Api,

Web, Mobile, or latex-service.



\## Target



.NET 10, `Nullable` and `ImplicitUsings` enabled.



\## Folder / namespace layout

CvTailr.Shared/

├── Cv/ -> CvTailr.Shared.Cv (CvDocument, CvRole, CvBullet, CvSkill)

├── Jd/ -> CvTailr.Shared.Jd (JdRequirements, JdRequirement)

├── Scoring/ -> CvTailr.Shared.Scoring (MatchScoreResult, RequirementMatch)

├── Ledger/ -> CvTailr.Shared.Ledger (LedgerEntry, LedgerStatus, DrillAttempt)

└── Enums/ -> CvTailr.Shared.Enums (RequirementPriority, ProficiencyLevel)





One class/enum per file, filename matches type name. Namespace matches

folder name exactly (no extra nesting).



\## Modeling conventions



\- \*\*IDs are `string` GUIDs\*\* (`Guid.NewGuid().ToString()`), not `Guid`.

&#x20; Reason: these types cross HTTP/JSON boundaries to non-.NET clients

&#x20; eventually (Web/Mobile may not stay .NET) — plain strings serialize

&#x20; unambiguously everywhere.

\- \*\*Dates use `DateOnly`\*\*, not `DateTime`, for anything that's a calendar

&#x20; date without a time component (e.g. `CvRole.StartDate`). Use

&#x20; `DateTimeOffset` (UTC) for actual timestamps (e.g. `LedgerEntry.LastReviewed`,

&#x20; `DrillAttempt.Timestamp`).

\- \*\*Collections are always initialized\*\*, never nullable

&#x20; (`List<T> Foo { get; set; } = new();`), so consumers never null-check

&#x20; a list. Use nullable only for genuinely optional scalar fields

&#x20; (e.g. `CvRole.EndDate` for a current role, `CvBullet.TailoredText`

&#x20; before a rewrite exists).

\- \*\*No computed business logic beyond trivial derived properties/helpers\*\*

&#x20; directly relevant to the type itself (e.g. `MatchScoreResult.MissingMustHaves`,

&#x20; `LedgerEntry.HasRecentWeakPerformance()`). Anything more involved

&#x20; (actual scoring algorithm, tailoring logic, drill-question generation)

&#x20; belongs in `CvTailr.Api`'s service layer, not here.

\- \*\*Enums over strings\*\* for any fixed, known set of states

&#x20; (`LedgerStatus`, `DrillOutcome`, `RequirementPriority`,

&#x20; `ProficiencyLevel`) — do not represent these as raw strings.



\## Rules this project must respect (see root CLAUDE.md for full detail)



\- `CvRole.CompanyName`, `CvRole.StartDate`, `CvRole.EndDate` must remain

&#x20; the only place company/date info lives on a role — never duplicate

&#x20; these onto `CvBullet` or elsewhere in a way that could let a rewrite

&#x20; operation touch them indirectly.

\- `CvBullet` and `CvSkill` both carry `IsProvisional` — additions from

&#x20; JD-driven tailoring must be flagged here so `CvTailr.Api` can create a

&#x20; matching `LedgerEntry` and never treat provisional content as

&#x20; equivalent to original CV content.

\- `CvBullet.Language` is constrained conceptually to Python, C#,

&#x20; TypeScript, Java when set via the substitution rule — this project

&#x20; does not enforce that constraint itself (no validation logic lives

&#x20; here), but any code touching this field in `CvTailr.Api` must.



\## What NOT to add here



\- No Azure SDK types, no Cosmos DB attributes, no HTTP/ASP.NET

&#x20; dependencies. Keep this project trivially portable and dependency-free.

\- No parsing logic (LaTeX or otherwise) — models only.

\- No validation attributes tied to a specific framework (e.g. avoid

&#x20; `System.ComponentModel.DataAnnotations` unless a real need comes up;

&#x20; prefer keeping this a plain POCO library).

