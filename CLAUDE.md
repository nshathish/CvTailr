# CVTailr

CVTailr matches a candidate's CV against a job description (JD), scores the
fit, tailors the CV to the JD, identifies skill gaps with a study plan, and
continuously drills the candidate via interview practice — feeding drill
results back into whether tailored additions stay on the CV.

## Architecture

Independent projects, each with its own solution/build tooling, living as
sibling folders in one repo. No shared root .sln. Communication between
projects is either:
- Direct .NET project reference to `CvTailr.Shared` (any .NET project —
  Api, and currently Web/Blazor and Mobile/MAUI too), or
- HTTP/JSON against Api's documented (OpenAPI) contract (any non-.NET
  piece, e.g. `latex-service`, or a future non-.NET UI).

`CvTailr.Shared` exists specifically to be reused across every .NET
project in this repo — that's the point of pulling these models into
their own class library rather than duplicating them per project. A
direct `ProjectReference` from Web or Mobile into Shared is fine as
long as they stay .NET (Blazor, MAUI). This does mean today's Web/
Mobile compile against real shared .NET types rather than treating
Api's JSON contract as an enforced boundary — that's accepted for now.

**If/when Web or Mobile is ever rewritten in a non-.NET stack** (Blazor
-> React, MAUI -> Flutter), that project can no longer reference
Shared, and will need local DTOs mirroring whatever Shared types it
used, talking to Api over HTTP/JSON only. Treat that as a dedicated,
deliberate migration task at that time — not something to pre-emptively
half-do now by avoiding the reference or hand-duplicating types "just
in case."

Projects:
- `CvTailr.Shared` — core domain models (CvDocument, JdRequirements,
  MatchScoreResult, LedgerEntry). .NET class library.
- `CvTailr.Api` — .NET 10 Minimal API. The "brain": parsing, scoring,
  tailoring, ledger, drill-question generation. References Shared directly.
- `CvTailr.Web` — Blazor app. CV/JD authoring, score review, tailoring
  diff approval. Talks to Api over HTTP only.
- `CvTailr.Mobile` — MAUI (or Flutter) app. Voice-based interview drilling.
  Talks to Api over HTTP only.
- `latex-service` — Python Azure Function. Parses .tex -> structured JSON
  and structured JSON -> .tex. The only non-.NET, non-UI piece.

## Tech stack

- Backend: .NET 10, ASP.NET Core Minimal API (no MVC controllers)
- LLM: Azure AI Foundry (model deployment + inference)
- Voice: Azure AI Speech (STT/TTS)
- Database: Azure Cosmos DB (ledger + parsed CV/JD documents)
- Auth: Entra ID / JWT bearer (both Web and Mobile are API clients)
- Web/desktop UI: Blazor
- Mobile UI: .NET MAUI (candidate for replacement with Flutter if voice
  streaming support proves inadequate — do not assume this is final)
- LaTeX parsing: Python (pylatexenc / TexSoup), isolated to latex-service

## Hard business rules — never violate these

1. **Dates and company names are immutable.** No tailoring, rewrite, or
   "language substitution" operation may ever modify `CvRole.CompanyName`,
   `CvRole.StartDate`, or `CvRole.EndDate`. These fields must never appear
   as targets in any rewrite/diff operation.

2. **Language substitution is scoped to exactly four languages:** Python,
   C#, TypeScript, Java. If a JD emphasizes one of these, bullets *within
   a role* may be reworded to reflect that language instead of the
   original one used in that role. Never substitute any other language,
   and never substitute if the JD doesn't emphasize one of these four.

3. **New experience or skills not in the original CV must always be
   proposed to the user first.** Never silently add content. If approved,
   the addition is marked `IsProvisional = true` (see CvBullet.cs /
   CvSkill.cs) and a corresponding `LedgerEntry` is created with
   `Status = Provisional`.

4. **Downgrades/removals of provisional content are recommendations, not
   automatic actions.** When a `LedgerEntry.HasRecentWeakPerformance()`
   check (or equivalent logic) trips, the system proposes removing or
   revising the item and waits for explicit user confirmation before any
   CV regeneration happens. Never auto-remove.

5. **Graduation:** a provisional item that performs consistently well in
   drills may be proposed for promotion to `Status = Confirmed`, again
   with user confirmation, not automatically.

## Repo conventions

- Each project folder has its own `.sln` (or equivalent) and can be
  opened/built independently.
- `CvTailr.Shared` types use `string` GUIDs (not `Guid`) for all IDs, for
  clean JSON/HTTP serialization across service boundaries.
- Favor thin Minimal API endpoint handlers that delegate to service
  classes — do not put business logic directly in route lambdas.
- Every subproject may have its own `CLAUDE.md` with project-specific
  detail; this root file holds only cross-cutting context. Check for a
  local `CLAUDE.md` in whichever project folder you're working in.
- After completing a task (a feature, a bug fix, a diagnostic session),
  write a short markdown summary of what was done and save it as
  `docs/<NNN>-<short-description>.md` in whichever project folder the
  work happened in (e.g. `CvTailr.Api/docs/`). `<NNN>` is a zero-padded,
  incrementing sequence number — numbering is per-project `docs` folder,
  not repo-wide, so check the current highest number there first. Match
  the prose style of existing docs in that folder (symptom/diagnosis/fix/
  verification-style headers).

## Known open design decisions

These are deliberate, documented gaps — not oversights. Resolve them
when the referenced trigger condition is reached, not before.

### Removed provisional content is stripped from the job's TailoredCvDocument (resolved)

This used to be an open gap: `LedgerEntry` downgraded to
`Status = Removed` had nothing to actually remove the bullet/skill
from, since `CvDocument` was passed statelessly and unpersisted.

As of the ledger hierarchical-partitioning task (`LedgerEntry` scoped
per `(cvId, jobId)`, alongside the per-job `TailoredCvDocument` from the
preceding task), `/api/ledger/{entryId}/confirm-status` with
`Status = Removed` now has `ILedgerService` strip the corresponding
bullet/skill out of that job's `TailoredCvDocument` (via
`ITailoredCvRepository.UpsertAsync`). The master `CvDocument` is never
touched. `Status = Confirmed` similarly updates that item's
`IsProvisional` flag to `false`, scoped to the same job's
`TailoredCvDocument` only — see the next section.

### Confirmed provisional items stay scoped to the job they were tailored for (deliberate)

When a `LedgerEntry` is confirmed (`Status = Confirmed`), the change is
scoped entirely to that job's `TailoredCvDocument` — it does **not**
merge back into the master `CvDocument`. Confirming an item is a
statement about how that item performed under drilling for *this job's*
tailoring, not a promotion into every future job's starting point; a
different job tailored from the same master CV starts from the same
provisional state again.

This is a deliberate design choice, not a limitation to fix later — do
not "fix" this into a master-CV merge without an explicit, re-discussed
decision to do so.
