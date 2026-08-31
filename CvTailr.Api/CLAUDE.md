# CvTailr.Api

.NET 10 Minimal API — the "brain" of CVTailr. Owns JD/CV parsing
orchestration, scoring, tailoring, the ledger, and drill-question
generation. References `CvTailr.Shared` directly (project reference).
Everything else (Web, Mobile) talks to this project over HTTP/JSON only.

## Target

.NET 10, `Nullable` and `ImplicitUsings` enabled. Minimal API — no MVC
controllers, no Razor Pages.

## Folder layout

CvTailr.Api/
├── Program.cs -> composition root: DI, middleware, route groups
├── Endpoints/
│ ├── JdEndpoints.cs -> MapGroup("/jd")
│ ├── CvEndpoints.cs -> MapGroup("/cv")
│ ├── ScoringEndpoints.cs -> MapGroup("/score")
│ ├── TailoringEndpoints.cs -> MapGroup("/tailor")
│ ├── LedgerEndpoints.cs -> MapGroup("/ledger")
│ └── DrillEndpoints.cs -> MapGroup("/drill")
├── Services/
│ ├── IJdParsingService.cs / JdParsingService.cs
│ ├── ICvParsingService.cs / CvParsingService.cs (calls latex-service)
│ ├── IScoringService.cs / ScoringService.cs
│ ├── ITailoringService.cs / TailoringService.cs
│ ├── ILedgerService.cs / LedgerService.cs
│ └── IDrillService.cs / DrillService.cs
├── Clients/
│ ├── FoundryClient.cs -> wraps Azure AI Foundry inference calls
│ ├── LatexServiceClient.cs -> HTTP client for the Python latex-service
│ └── SpeechClient.cs -> wraps Azure AI Speech (STT/TTS)
├── Data/
│ └── CosmosLedgerRepository.cs (+ interface)
├── tasks/
└── CvTailr.Api.csproj


## Endpoint conventions

- One `MapGroup` per resource area (`/jd`, `/cv`, `/score`, `/tailor`,
  `/ledger`, `/drill`), registered in `Program.cs` via an
  `app.MapXEndpoints()` extension method per group — do not register raw
  `app.MapPost(...)` calls directly in `Program.cs`.
- Route handlers are thin: deserialize/validate input, call exactly one
  service method, return the result. No business logic, no direct
  Cosmos/Foundry/HTTP calls inside a route lambda — those belong in
  `Services/` and `Clients/`.
- Request/response DTOs are the types from `CvTailr.Shared` wherever
  possible. Only introduce an Api-local DTO when a shape is genuinely
  endpoint-specific and doesn't belong in Shared (e.g. a paged wrapper).
- Use `TypedResults` (not bare `Results`) for endpoint return types so
  OpenAPI generation stays accurate — this matters because the OpenAPI
  spec is the real contract for Web/Mobile per the root CLAUDE.md.

## Services vs Clients

- **Clients/** wrap a specific external dependency (Azure AI Foundry,
  the Python latex-service, Azure AI Speech) and do nothing else — no
  business rules, just "send this, get that back," including
  retry/timeout handling for that dependency.
- **Services/** contain the actual orchestration and business rules
  (e.g. `TailoringService` enforces the language-substitution scope and
  the "never touch dates/companies" rule before calling `FoundryClient`;
  `LedgerService` implements the recommend-not-auto-downgrade logic on
  top of `LedgerEntry.HasRecentWeakPerformance()`).
- Register both as scoped services via DI in `Program.cs`; endpoints
  depend on `Services/` interfaces only, never on `Clients/` directly.

## Azure AI Foundry usage

- All model calls go through `Clients/FoundryClient.cs` — no other file
  calls the Foundry SDK directly.
- Prompts that produce structured data (JD requirement extraction,
  scoring rationale, drill question generation) must request/parse JSON
  output matching the relevant `CvTailr.Shared` type — never free-text
  parsing with regex on model output.
- Different tasks may use different deployed models (e.g. a smaller/
  cheaper model for drill-question generation vs a stronger one for
  scoring) — `FoundryClient` should accept a deployment/model name
  parameter rather than hardcoding one model for every call.

## Enforcing the hard business rules (see root CLAUDE.md)

This project is where the hard rules actually get enforced in code, not
just documented:

- `TailoringService` must reject or strip any proposed edit that targets
  `CvRole.CompanyName`, `CvRole.StartDate`, or `CvRole.EndDate`. Treat
  this as a validation step on whatever the Foundry call returns —
  never trust the model's output to have respected the rule on its own.
- `TailoringService` must validate that any `CvBullet.Language`
  substitution is one of exactly `"Python"`, `"C#"`, `"TypeScript"`,
  `"Java"` before applying it, and only when that language appears in
  `JdRequirements.EmphasizedLanguages`.
- New bullets/skills proposed by tailoring are returned to the caller as
  a **proposal**, not persisted — persistence (and marking
  `IsProvisional = true` + creating a `LedgerEntry`) only happens via a
  separate explicit "approve" endpoint call from the client (Web/Mobile),
  never automatically as part of the tailoring response.
- `LedgerService`'s downgrade/promotion logic returns a recommendation
  object; it must not mutate `LedgerEntry.Status` itself. A separate
  explicit confirmation endpoint performs the actual status change.

## Auth

- JWT bearer auth (Entra ID) on all endpoints except health checks.
  Both `CvTailr.Web` and `CvTailr.Mobile` authenticate as API clients —
  do not build cookie-based auth, since Mobile has no browser session.

## What NOT to do here

- No direct Cosmos/Foundry/Speech SDK calls outside `Clients/`.
- No LaTeX parsing logic in this project — always delegate to
  `latex-service` via `LatexServiceClient`.
- No MVC controllers, no Razor.