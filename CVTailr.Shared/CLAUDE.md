# CvTailr.Shared

Core domain models shared between `CvTailr.Api` and (only via HTTP/OpenAPI
contract, never direct reference) the UI-layer projects. This is a plain
.NET class library — no business logic, no I/O, no dependencies on Api,
Web, Mobile, or latex-service.

## Target

.NET 10, `Nullable` and `ImplicitUsings` enabled.

## Folder / namespace layout

CvTailr.Shared/
├── Cv/ -> CvTailr.Shared.Cv (CvDocument, CvRole, CvBullet, CvSkill)
├── Jd/ -> CvTailr.Shared.Jd (JdRequirements, JdRequirement)
├── Scoring/ -> CvTailr.Shared.Scoring (MatchScoreResult, RequirementMatch)
├── Ledger/ -> CvTailr.Shared.Ledger (LedgerEntry, LedgerStatus, DrillAttempt)
└── Enums/ -> CvTailr.Shared.Enums (RequirementPriority, ProficiencyLevel)


One class/enum per file, filename matches type name. Namespace matches
folder name exactly (no extra nesting).

## Modeling conventions

- **IDs are `string` GUIDs** (`Guid.NewGuid().ToString()`), not `Guid`.
  Reason: these types cross HTTP/JSON boundaries to non-.NET clients
  eventually (Web/Mobile may not stay .NET) — plain strings serialize
  unambiguously everywhere.
- **Dates use `DateOnly`**, not `DateTime`, for anything that's a calendar
  date without a time component (e.g. `CvRole.StartDate`). Use
  `DateTimeOffset` (UTC) for actual timestamps (e.g. `LedgerEntry.LastReviewed`,
  `DrillAttempt.Timestamp`).
- **Collections are always initialized**, never nullable
  (`List<T> Foo { get; set; } = new();`), so consumers never null-check
  a list. Use nullable only for genuinely optional scalar fields
  (e.g. `CvRole.EndDate` for a current role, `CvBullet.TailoredText`
  before a rewrite exists).
- **No computed business logic beyond trivial derived properties/helpers**
  directly relevant to the type itself (e.g. `MatchScoreResult.MissingMustHaves`,
  `LedgerEntry.HasRecentWeakPerformance()`). Anything more involved
  (actual scoring algorithm, tailoring logic, drill-question generation)
  belongs in `CvTailr.Api`'s service layer, not here.
- **Enums over strings** for any fixed, known set of states
  (`LedgerStatus`, `DrillOutcome`, `RequirementPriority`,
  `ProficiencyLevel`) — do not represent these as raw strings.

## Rules this project must respect (see root CLAUDE.md for full detail)

- `CvRole.CompanyName`, `CvRole.StartDate`, `CvRole.EndDate` must remain
  the only place company/date info lives on a role — never duplicate
  these onto `CvBullet` or elsewhere in a way that could let a rewrite
  operation touch them indirectly.
- `CvBullet` and `CvSkill` both carry `IsProvisional` — additions from
  JD-driven tailoring must be flagged here so `CvTailr.Api` can create a
  matching `LedgerEntry` and never treat provisional content as
  equivalent to original CV content.
- `CvBullet.Language` is constrained conceptually to Python, C#,
  TypeScript, Java when set via the substitution rule — this project
  does not enforce that constraint itself (no validation logic lives
  here), but any code touching this field in `CvTailr.Api` must.

## What NOT to add here

- No Azure SDK types, no Cosmos DB attributes, no HTTP/ASP.NET
  dependencies. Keep this project trivially portable and dependency-free.
- No parsing logic (LaTeX or otherwise) — models only.
- No validation attributes tied to a specific framework (e.g. avoid
  `System.ComponentModel.DataAnnotations` unless a real need comes up;
  prefer keeping this a plain POCO library).