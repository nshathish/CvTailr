# CVTailr

CVTailr matches a candidate's CV against a job description (JD), scores the
fit, tailors the CV to the JD, identifies skill gaps with a study plan, and
continuously drills the candidate via interview practice — feeding drill
results back into whether tailored additions stay on the CV.

## Architecture

Independent projects, each with its own solution/build tooling, living as
sibling folders in one repo. No shared root .sln. Communication between
projects is either:
- Direct .NET project reference (only Api <-> Shared, both .NET), or
- HTTP/JSON against Api's documented (OpenAPI) contract (everything else).

This is deliberate: UI-layer projects (Web, Mobile) must stay swappable
(e.g. Blazor -> React, MAUI -> Flutter) without depending on shared .NET
types. Never add a project reference from Web or Mobile into Shared.

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

## Known open design decisions

These are deliberate, documented gaps — not oversights. Resolve them
when the referenced trigger condition is reached, not before.

### Removed provisional content is not yet stripped from CvDocument

When a `LedgerEntry` is downgraded to `Status = Removed` (via
`/api/ledger/{entryId}/confirm-status`), nothing currently removes the
corresponding bullet/skill from the `CvDocument` itself. `CvDocument`
is passed statelessly between API calls and is not yet persisted
anywhere (only `LedgerEntry` has Cosmos persistence, as of the ledger
task). Two options exist, and the choice depends on what triggers it:

- **Client-side filtering** (fits today's stateless design): any
  consumer that renders/exports a CV (Web app, future LaTeX
  regeneration step) must cross-reference `IsProvisional` bullets/
  skills against the current ledger state and exclude any whose
  matching `LedgerEntry.Status == Removed`. No API change required.
- **A `/api/tailor/revert` endpoint** that mutates a persisted
  `CvDocument` server-side, producing one canonical "current" document.
  This only makes sense once `CvDocument` itself is persisted in Cosmos
  (or elsewhere) — there's nothing to write back to yet.

**Decision trigger:** revisit this when `CvDocument` persistence is
built (expected to happen alongside `CvTailr.Web`, when a user needs to
save/reload a CV across sessions rather than re-parsing LaTeX every
time). Until then, default to client-side filtering if anything needs
to render a "current" CV state.
