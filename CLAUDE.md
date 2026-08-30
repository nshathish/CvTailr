\# CVTailr



CVTailr matches a candidate's CV against a job description (JD), scores the

fit, tailors the CV to the JD, identifies skill gaps with a study plan, and

continuously drills the candidate via interview practice — feeding drill

results back into whether tailored additions stay on the CV.



\## Architecture



Independent projects, each with its own solution/build tooling, living as

sibling folders in one repo. No shared root .sln. Communication between

projects is either:

\- Direct .NET project reference (only Api <-> Shared, both .NET), or

\- HTTP/JSON against Api's documented (OpenAPI) contract (everything else).



This is deliberate: UI-layer projects (Web, Mobile) must stay swappable

(e.g. Blazor -> React, MAUI -> Flutter) without depending on shared .NET

types. Never add a project reference from Web or Mobile into Shared.



Projects:

\- `CvTailr.Shared` — core domain models (CvDocument, JdRequirements,

&#x20; MatchScoreResult, LedgerEntry). .NET class library.

\- `CvTailr.Api` — .NET 10 Minimal API. The "brain": parsing, scoring,

&#x20; tailoring, ledger, drill-question generation. References Shared directly.

\- `CvTailr.Web` — Blazor app. CV/JD authoring, score review, tailoring

&#x20; diff approval. Talks to Api over HTTP only.

\- `CvTailr.Mobile` — MAUI (or Flutter) app. Voice-based interview drilling.

&#x20; Talks to Api over HTTP only.

\- `latex-service` — Python Azure Function. Parses .tex -> structured JSON

&#x20; and structured JSON -> .tex. The only non-.NET, non-UI piece.



\## Tech stack



\- Backend: .NET 10, ASP.NET Core Minimal API (no MVC controllers)

\- LLM: Azure AI Foundry (model deployment + inference)

\- Voice: Azure AI Speech (STT/TTS)

\- Database: Azure Cosmos DB (ledger + parsed CV/JD documents)

\- Auth: Entra ID / JWT bearer (both Web and Mobile are API clients)

\- Web/desktop UI: Blazor

\- Mobile UI: .NET MAUI (candidate for replacement with Flutter if voice

&#x20; streaming support proves inadequate — do not assume this is final)

\- LaTeX parsing: Python (pylatexenc / TexSoup), isolated to latex-service



\## Hard business rules — never violate these



1\. \*\*Dates and company names are immutable.\*\* No tailoring, rewrite, or

&#x20;  "language substitution" operation may ever modify `CvRole.CompanyName`,

&#x20;  `CvRole.StartDate`, or `CvRole.EndDate`. These fields must never appear

&#x20;  as targets in any rewrite/diff operation.



2\. \*\*Language substitution is scoped to exactly four languages:\*\* Python,

&#x20;  C#, TypeScript, Java. If a JD emphasizes one of these, bullets \*within

&#x20;  a role\* may be reworded to reflect that language instead of the

&#x20;  original one used in that role. Never substitute any other language,

&#x20;  and never substitute if the JD doesn't emphasize one of these four.



3\. \*\*New experience or skills not in the original CV must always be

&#x20;  proposed to the user first.\*\* Never silently add content. If approved,

&#x20;  the addition is marked `IsProvisional = true` (see CvBullet.cs /

&#x20;  CvSkill.cs) and a corresponding `LedgerEntry` is created with

&#x20;  `Status = Provisional`.



4\. \*\*Downgrades/removals of provisional content are recommendations, not

&#x20;  automatic actions.\*\* When a `LedgerEntry.HasRecentWeakPerformance()`

&#x20;  check (or equivalent logic) trips, the system proposes removing or

&#x20;  revising the item and waits for explicit user confirmation before any

&#x20;  CV regeneration happens. Never auto-remove.



5\. \*\*Graduation:\*\* a provisional item that performs consistently well in

&#x20;  drills may be proposed for promotion to `Status = Confirmed`, again

&#x20;  with user confirmation, not automatically.



\## Repo conventions



\- Each project folder has its own `.sln` (or equivalent) and can be

&#x20; opened/built independently.

\- `CvTailr.Shared` types use `string` GUIDs (not `Guid`) for all IDs, for

&#x20; clean JSON/HTTP serialization across service boundaries.

\- Favor thin Minimal API endpoint handlers that delegate to service

&#x20; classes — do not put business logic directly in route lambdas.

\- Every subproject may have its own `CLAUDE.md` with project-specific

&#x20; detail; this root file holds only cross-cutting context. Check for a

&#x20; local `CLAUDE.md` in whichever project folder you're working in.

