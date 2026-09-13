# CvTailr.Web

Blazor Web App — the authoring UI for CVTailr. This is where the JD
gets pasted in, CV gets parsed, scores get reviewed, and tailoring
proposals get approved/rejected. Talks to `CvTailr.Api` over HTTP/JSON
only — no project reference to `CvTailr.Api`. It DOES hold a direct
project reference to `CvTailr.Shared` and uses its types (`CvDocument`,
`Job`, `TailoringProposal`, `TailoredCvDocument`, etc.) throughout —
see root `CLAUDE.md`'s Architecture section: Shared is meant to be
reused by any .NET project, and this stays fine as long as Web is
Blazor. Only a move to a non-.NET UI stack would require dropping the
reference in favor of local mirrored DTOs, and that's a dedicated
future migration task, not today's concern.

## Target

.NET 10, Blazor Web App template.

## Render mode

No interactivity/render mode decision has been locked in yet — default
to whatever the Blazor Web App template scaffolded (static
server-rendered pages) until a specific page genuinely needs
interactivity (e.g. live-updating drill/score UI), at which point add
`@rendermode InteractiveServer` per-component rather than switching the
whole app to a different global render mode. Don't reach for WASM
unless there's a concrete reason (e.g. wanting the app to run without a
constantly-connected server) — Interactive Server is the simpler
default given this is a personal tool, not a public-facing app needing
offline/edge behavior.

## Auth

Real sign-in is wired via `Microsoft.Identity.Web` against the Entra
External ID (CIAM) tenant — see `docs/001-entraid-authentication.md` for
the full implementation. `Pages/App/` is protected by `[Authorize]`
(enforced via `AuthorizeRouteView` in `Routes.razor`), sign-in/sign-up/
sign-out all go through `MicrosoftIdentity/Account/*` endpoints, and every
typed Api client request carries a Bearer token acquired via
`ITokenAcquisition` (via `IDownstreamApi`, wired through `ApiClientBase`
— see below).

## How this project talks to the Api

- One typed client per `CvTailr.Api` endpoint group/`MapGroup`
  (`IJdApiClient`/`JdApiClient`, `ICvApiClient`/`CvApiClient`,
  `IJobsApiClient`/`JobsApiClient`, `IScoreApiClient`/`ScoreApiClient`,
  `ITailorApiClient`/`TailorApiClient`, and so on as new endpoint groups
  — Ledger, Drill — get Web-side callers). This mirrors the boundary
  Api itself already committed to (one `Services/` interface per
  endpoint group there) — a client covering every endpoint group in one
  fat class is the thing to avoid, not the default to reach for. Each
  concrete client implements an interface in `Services/Interfaces/`, and
  pages inject only the interface(s) they actually need via
  constructor/`@inject` — never the concrete class.
- All of them derive from `Services/ApiClientBase.cs`, which holds the
  shared plumbing: `IDownstreamApi` access, the wire-format
  `JsonSerializerOptions` (camelCase, string enums, matching Api's own
  `JsonStringEnumConverter` setup), and `ThrowIfUnsuccessfulAsync` for
  the common "non-success status -> throw `ApiClientException`"
  pattern. A method needing special status handling (e.g. 404 -> `null`,
  or a distinct outcome type) checks that itself before calling
  `ThrowIfUnsuccessfulAsync`.
- No page/component should construct its own `HttpClient`/call
  `IDownstreamApi` directly, or call `Api` endpoints outside these
  typed clients — so request/response shapes and error handling for a
  given endpoint group stay in one place.
- Request/response shapes reuse `CvTailr.Shared` types directly (JD,
  CV, MatchScoreResult, TailoringProposal, LedgerEntry, DrillQuestion,
  etc.) via the project reference — do not hand-declare a parallel
  local copy of a type that already exists in Shared. Only declare a
  new type locally, next to the client that returns it (e.g.
  `ScoreOutcome` in `ScoreApiClient.cs`, `JobCvResponse` in
  `JobsApiClient.cs`), when the shape is genuinely Web/endpoint-specific
  and doesn't belong in Shared — a small wrapper/result record that
  mirrors an Api endpoint's response envelope, not a domain model.
- Registered individually in `Program.cs`
  (`AddScoped<IJdApiClient, JdApiClient>()` etc.) — adding a new
  endpoint group means adding both its client class and its DI
  registration, not a new method on an existing shared class.

## Folder layout

CvTailr.Web/
├── Program.cs
├── Services/
│ ├── ApiClientBase.cs -> shared plumbing (auth, JSON options, error handling) for every typed client
│ ├── ApiClientException.cs
│ ├── JdApiClient.cs / CvApiClient.cs / JobsApiClient.cs / ScoreApiClient.cs / TailorApiClient.cs -> one per Api endpoint group
│ ├── Interfaces/ -> IJdApiClient.cs, ICvApiClient.cs, IJobsApiClient.cs, IScoreApiClient.cs, ITailorApiClient.cs
│ └── WorkflowStateService.cs
├── Components/
│ ├── Pages/ -> routable pages (Jd input, Score review, Tailor review, etc.)
│ └── Shared/ -> reusable UI pieces (score badge, diff viewer, etc.)
├── wwwroot/
└── appsettings.json


## Navigation links

- Use `<NavLink>`, not a plain `<a>`, for links to this app's own Blazor
  routes (nav items, in-app CTAs) — it participates in the router and
  applies an `active` CSS class on match, which `<a>` never does.
- Plain `<a>` is the correct exception for anything that isn't a routable
  `@page` component in this app: the Entra External ID challenge/sign-out
  endpoints (`MicrosoftIdentity/Account/SignIn`, `/account/logout`), or any
  other external/controller-routed redirect. Add
  `data-enhance-nav="false"` on those so Blazor's enhanced navigation
  doesn't try to intercept the redirect. See
  `docs/001-entraid-authentication.md` for why the auth links work this way.

## UI/workflow conventions

- The authoring flow maps directly onto the Api's endpoint sequence:
  JD paste -> `/jd/parse`, CV parse -> `/cv/parse`, score ->
  `/score`, tailoring review -> `/tailor/propose` then
  `/tailor/apply`. Build pages in that order — don't build a
  tailoring-review page before the score page exists and works, since
  each step's output feeds the next.
- Tailoring approval (bullet rewrites, new bullets, new skills) must be
  presented as an explicit per-item choice (checkbox/approve-reject per
  proposed change) — never a single "apply all" button, since
  per-item approval is a hard rule from the root CLAUDE.md, not just a
  nice-to-have UI pattern.
- No page should hold parsed JD/CV/score state only in local component
  fields that vanish on navigation — until a proper state/session store
  exists, pass state forward between pages explicitly (e.g. via
  navigation parameters or a scoped state container), so a refresh
  doesn't silently lose a completed step's output. A simple scoped
  `WorkflowStateService` (in-memory, per-session) is enough for this
  phase — no need for persistence yet.

## What NOT to do here

- No direct Cosmos, Foundry, or latex-service calls — everything goes
  through `CvTailr.Api`.
- No project reference to `CvTailr.Api` (HTTP/JSON only, via the typed
  Api clients). A reference to `CvTailr.Shared` is fine — see above.
- No hardcoded Api URLs in components — always via configuration and
  the typed Api clients.
- No single client covering multiple Api endpoint groups — see "How
  this project talks to the Api" above. A page needing more than one
  resource area injects more than one client; that's the expected
  shape, not a sign to merge them back together.

## Styling

Tailwind CSS is the styling approach for this project — no Bootstrap
(even though the Blazor Web App template scaffolds it by default), no
component library, no hand-rolled global CSS beyond Tailwind's own
setup files.

- Remove the template's default Bootstrap references
  (`bootstrap/bootstrap.min.css` link in the host page, and the
  default `.css` files that style against Bootstrap classes) as part
  of initial setup — don't leave both in place.
- Tailwind is wired via its CLI (or the `@tailwindcss/cli`/PostCSS
  pipeline, whichever is straightforward with the current Tailwind
  version) watching `Components/**/*.razor` and outputting to
  `wwwroot/css/app.css`, which the host page references.
- Use Tailwind utility classes directly in `.razor` markup. Do not
  introduce a separate custom class per component unless a pattern is
  genuinely repeated enough to warrant `@apply` in a small shared CSS
  file — default to inline utilities.
- No inline `style="..."` attributes for anything Tailwind can express.