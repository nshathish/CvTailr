# CvTailr.Api

.NET 10 Minimal API — the "brain" of CVTailr. Owns CV parsing, scoring,
tailoring, the ledger, and drill-question generation directly; JD
parsing itself lives in the separate `CvTailr.Jd.Api` service, which
this project calls internally (see `Features/Jobs/JdServiceClient.cs`)
and which is never exposed to Web/Mobile directly — they only ever see
this project's `POST /api/jobs/parse`. References `CvTailr.Shared`
directly (project reference). Everything else (Web, Mobile) talks to
this project over HTTP/JSON only.

## Target

.NET 10, `Nullable` and `ImplicitUsings` enabled. Minimal API — no MVC
controllers, no Razor Pages.

## Folder layout

This project is organized as vertical feature slices, not technical
layers — each feature folder under `Features/` owns its endpoint,
service(s), repository, and any feature-specific exception together,
rather than those being split across parallel `Endpoints/`/`Services/`/
`Data/` folders. `Common/` holds only what three or more features
actually share.

CvTailr.Api/
├── Program.cs -> composition root: calls `AddCommonInfrastructure()` +
│   one `AddXFeature()` per feature (see DependencyInjection.cs), then
│   wires middleware and each feature's `MapXEndpoints()`
├── DependencyInjection.cs -> every DI registration lives here:
│   `AddCommonInfrastructure()` plus `AddCvFeature()`, `AddJobsFeature()`,
│   `AddScoringFeature()`, `AddTailoringFeature()`, `AddLedgerFeature()`,
│   `AddDrillFeature()` — `Program.cs` never registers a type directly
├── Features/
│ ├── Cv/        -> CvEndpoints.cs (MapGroup("/cv")), CvParsingService
│ │                 (calls latex-service), CvSourceExtractor,
│ │                 CosmosCvRepository, UnsupportedCvFormatException
│ ├── Jobs/      -> JobEndpoints.cs (MapGroup("/api/jobs")), JobService,
│ │                 IJobRepository/CosmosJobRepository — a user's own
│ │                 saved Jobs. `POST /api/jobs/parse` is the only
│ │                 client-facing JD-parsing entry point: it calls
│ │                 IJdServiceClient/JdServiceClient (a typed HttpClient,
│ │                 base address from JdApiOptions/"JdApi:BaseUrl") to
│ │                 reach the separate `CvTailr.Jd.Api` service, then
│ │                 persists the result via JobService.CreateFromJdAsync.
│ │                 Web/Mobile never call Jd.Api directly — do not
│ │                 re-add a `/jd` or `/api/jd` endpoint group here.
│ │                 Listings/ -> JobListingEndpoints.cs
│ │                 (MapGroup("/api/job-listings")), JobListingsService,
│ │                 JobListingCaptureService, the static helpers
│ │                 JobListingMatcher/JobListingUrlHelper/
│ │                 CompanyDomainResolver/ExcludedCompanyDomains, and
│ │                 IJobListingRepository/CosmosJobListingRepository —
│ │                 the user-independent shared JobListing cache. Split
│ │                 into its own sub-namespace
│ │                 (Features.Jobs.Listings) once Jobs/ grew past ten
│ │                 files covering two distinct concerns — still a
│ │                 vertical (by capability) split, not a horizontal
│ │                 Endpoints/Services/Repositories one; don't introduce
│ │                 those layers inside a feature folder.
│ ├── Scoring/   -> ScoringEndpoints.cs (MapGroup("/score")), ScoringService
│ ├── Tailoring/ -> TailoringEndpoints.cs (MapGroup("/tailor")), TailoringService
│ ├── Ledger/    -> LedgerEndpoints.cs (MapGroup("/ledger")), LedgerService,
│ │                 CosmosLedgerRepository
│ └── Drill/     -> DrillEndpoints.cs (MapGroup("/drill")), DrillService
├── Common/ -> used by 3+ features — don't add something here
│ │            pre-emptively; start it inside the one feature that needs
│ │            it and promote it only once a second feature genuinely
│ │            needs the same thing
│ ├── Auth/         -> CurrentUserContext, EntraIdOptions
│ ├── Foundry/      -> FoundryClient (wraps Azure AI Foundry inference
│ │                    calls), FoundryOptions
│ ├── SkillTagging/ -> SkillTaggingService + TagCanonicalizer (used by
│ │                    Cv and Jobs)
│ ├── TailoredCv/   -> CosmosTailoredCvRepository +
│ │                    CvDocumentResolutionExtensions (used by Jobs,
│ │                    Scoring, Tailoring, Ledger, Drill)
│ └── Cosmos/       -> CosmosOptions, CosmosContainerProvisioner
├── tasks/
└── CvTailr.Api.csproj

A Latex/Speech client (per the root CLAUDE.md tech stack) would live
under its own `Common/` subfolder once added — neither exists yet.

Full rationale/history for this layout is in
`../.docs/api-services-simplification-options.md` at the solution root.

## Endpoint conventions

- One `MapGroup` per resource area (`/api/jobs`, `/cv`, `/score`,
  `/tailor`, `/ledger`, `/drill`), each living in that feature's own
  `Features/<Name>/<Name>Endpoints.cs` and registered in `Program.cs`
  via an `app.MapXEndpoints()` call per feature — do not register raw
  `app.MapPost(...)` calls directly in `Program.cs`.
- Route handlers are thin: deserialize/validate input, call exactly one
  service method, return the result. No business logic, no direct
  Cosmos/Foundry/HTTP calls inside a route lambda — those belong in the
  feature's own service classes, or `Common/` for cross-feature infra.
- Request/response DTOs are the types from `CvTailr.Shared` wherever
  possible. Only introduce an Api-local DTO when a shape is genuinely
  endpoint-specific and doesn't belong in Shared (e.g. a paged wrapper).
- Use `TypedResults` (not bare `Results`) for endpoint return types so
  OpenAPI generation stays accurate — this matters because the OpenAPI
  spec is the real contract for Web/Mobile per the root CLAUDE.md.

## Services vs internal collaborators

Not every class in a feature folder is a DI service — three tiers:

- **Interface + scoped DI registration** (in `DependencyInjection.cs`,
  one `AddXFeature()` per feature) when something outside the class
  needs to construct or substitute it — another feature/service, or a
  test double. E.g. `TailoringService` enforces the
  language-substitution scope and the "never touch dates/companies"
  rule before calling `FoundryClient`; `LedgerService` implements the
  recommend-not-auto-downgrade logic on top of
  `LedgerEntry.HasRecentWeakPerformance()`.
- **Concrete type registered, no interface** when DI still needs to
  construct it (e.g. it needs a typed `HttpClient`) but it has exactly
  one caller and no interface is pulling its weight. `JdServiceClient`
  (`Features/Jobs/JdServiceClient.cs`) is the one exception to this
  tier in practice — it's registered behind `IJdServiceClient` even
  though it has one caller, specifically so a test double can stand in
  for the real `CvTailr.Jd.Api` HTTP call in tests.
- **Not registered at all** when it has no constructor dependencies DI
  needs to supply — its one caller just `new()`s it (e.g.
  `JdSourceResolver` instantiating `JdHtmlExtractor` directly) or calls
  it as a static helper (e.g. `TagCanonicalizer`,
  `JobListingUrlHelper`).

Default to the narrowest tier that works; only add an interface once a
second caller or a test double actually needs the seam.

## Azure AI Foundry usage

- All model calls go through `Common/Foundry/FoundryClient.cs` — no
  other file calls the Foundry SDK directly.
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

- No direct Foundry/Speech SDK calls outside `Common/Foundry` (Foundry)
  or a future `Common/Speech` — and no direct Cosmos SDK calls outside
  each feature's own `CosmosXRepository` / `Common/Cosmos`.
- No LaTeX parsing logic in this project — always delegate to
  `latex-service` via a dedicated HTTP client under `Common/` once one
  is added (see root CLAUDE.md tech stack) — never inline here.
- No MVC controllers, no Razor.