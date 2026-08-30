\# CvTailr.Api



.NET 10 Minimal API — the "brain" of CVTailr. Owns JD/CV parsing

orchestration, scoring, tailoring, the ledger, and drill-question

generation. References `CvTailr.Shared` directly (project reference).

Everything else (Web, Mobile) talks to this project over HTTP/JSON only.



\## Target



.NET 10, `Nullable` and `ImplicitUsings` enabled. Minimal API — no MVC

controllers, no Razor Pages.



\## Folder layout

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





\## Endpoint conventions



\- One `MapGroup` per resource area (`/jd`, `/cv`, `/score`, `/tailor`,

&#x20; `/ledger`, `/drill`), registered in `Program.cs` via an

&#x20; `app.MapXEndpoints()` extension method per group — do not register raw

&#x20; `app.MapPost(...)` calls directly in `Program.cs`.

\- Route handlers are thin: deserialize/validate input, call exactly one

&#x20; service method, return the result. No business logic, no direct

&#x20; Cosmos/Foundry/HTTP calls inside a route lambda — those belong in

&#x20; `Services/` and `Clients/`.

\- Request/response DTOs are the types from `CvTailr.Shared` wherever

&#x20; possible. Only introduce an Api-local DTO when a shape is genuinely

&#x20; endpoint-specific and doesn't belong in Shared (e.g. a paged wrapper).

\- Use `TypedResults` (not bare `Results`) for endpoint return types so

&#x20; OpenAPI generation stays accurate — this matters because the OpenAPI

&#x20; spec is the real contract for Web/Mobile per the root CLAUDE.md.



\## Services vs Clients



\- \*\*Clients/\*\* wrap a specific external dependency (Azure AI Foundry,

&#x20; the Python latex-service, Azure AI Speech) and do nothing else — no

&#x20; business rules, just "send this, get that back," including

&#x20; retry/timeout handling for that dependency.

\- \*\*Services/\*\* contain the actual orchestration and business rules

&#x20; (e.g. `TailoringService` enforces the language-substitution scope and

&#x20; the "never touch dates/companies" rule before calling `FoundryClient`;

&#x20; `LedgerService` implements the recommend-not-auto-downgrade logic on

&#x20; top of `LedgerEntry.HasRecentWeakPerformance()`).

\- Register both as scoped services via DI in `Program.cs`; endpoints

&#x20; depend on `Services/` interfaces only, never on `Clients/` directly.



\## Azure AI Foundry usage



\- All model calls go through `Clients/FoundryClient.cs` — no other file

&#x20; calls the Foundry SDK directly.

\- Prompts that produce structured data (JD requirement extraction,

&#x20; scoring rationale, drill question generation) must request/parse JSON

&#x20; output matching the relevant `CvTailr.Shared` type — never free-text

&#x20; parsing with regex on model output.

\- Different tasks may use different deployed models (e.g. a smaller/

&#x20; cheaper model for drill-question generation vs a stronger one for

&#x20; scoring) — `FoundryClient` should accept a deployment/model name

&#x20; parameter rather than hardcoding one model for every call.



\## Enforcing the hard business rules (see root CLAUDE.md)



This project is where the hard rules actually get enforced in code, not

just documented:



\- `TailoringService` must reject or strip any proposed edit that targets

&#x20; `CvRole.CompanyName`, `CvRole.StartDate`, or `CvRole.EndDate`. Treat

&#x20; this as a validation step on whatever the Foundry call returns —

&#x20; never trust the model's output to have respected the rule on its own.

\- `TailoringService` must validate that any `CvBullet.Language`

&#x20; substitution is one of exactly `"Python"`, `"C#"`, `"TypeScript"`,

&#x20; `"Java"` before applying it, and only when that language appears in

&#x20; `JdRequirements.EmphasizedLanguages`.

\- New bullets/skills proposed by tailoring are returned to the caller as

&#x20; a \*\*proposal\*\*, not persisted — persistence (and marking

&#x20; `IsProvisional = true` + creating a `LedgerEntry`) only happens via a

&#x20; separate explicit "approve" endpoint call from the client (Web/Mobile),

&#x20; never automatically as part of the tailoring response.

\- `LedgerService`'s downgrade/promotion logic returns a recommendation

&#x20; object; it must not mutate `LedgerEntry.Status` itself. A separate

&#x20; explicit confirmation endpoint performs the actual status change.



\## Auth



\- JWT bearer auth (Entra ID) on all endpoints except health checks.

&#x20; Both `CvTailr.Web` and `CvTailr.Mobile` authenticate as API clients —

&#x20; do not build cookie-based auth, since Mobile has no browser session.



\## What NOT to do here



\- No direct Cosmos/Foundry/Speech SDK calls outside `Clients/`.

\- No LaTeX parsing logic in this project — always delegate to

&#x20; `latex-service` via `LatexServiceClient`.

\- No MVC controllers, no Razor.

