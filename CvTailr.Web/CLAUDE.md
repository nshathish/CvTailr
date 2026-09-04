# CvTailr.Web

Blazor Web App — the authoring UI for CVTailr. This is where the JD
gets pasted in, CV gets parsed, scores get reviewed, and tailoring
proposals get approved/rejected. Talks to `CvTailr.Api` over HTTP/JSON
only — no project reference to `CvTailr.Api` or `CvTailr.Shared` (per
root CLAUDE.md's rule that UI-layer projects must stay swappable).

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
`ApiClient` request carries a Bearer token acquired via `ITokenAcquisition`.

## How this project talks to the Api

- All Api calls go through a single typed `ApiClient` (or similarly
  named) class wrapping `HttpClient`, registered via
  `builder.Services.AddHttpClient<ApiClient>(...)` in `Program.cs` with
  `BaseAddress` read from configuration (`Api:BaseUrl` in
  `appsettings.json`, pointing at the local Api's `https://localhost:<port>`
  during this phase).
- No page/component should construct its own `HttpClient` or call
  `Api` endpoints directly — always go through `ApiClient`, so the
  request/response shapes and error handling live in one place.
- Request/response shapes mirror `CvTailr.Shared` types exactly (JD,
  CV, MatchScoreResult, TailoringProposal, LedgerEntry, DrillQuestion,
  etc.) even though this project has no reference to that assembly —
  redeclare matching DTOs locally in a `Models/` folder here. Keep
  field names and casing identical to avoid silent (de)serialization
  mismatches. If the Api's OpenAPI spec is available, prefer generating
  these DTOs from it rather than hand-typing, if that's easy to set up;
  otherwise hand-type carefully and note this in code as a
  spec-generation TODO.

## Folder layout

CvTailr.Web/
├── Program.cs
├── Models/ -> local DTOs mirroring CvTailr.Shared shapes
├── Services/
│ └── ApiClient.cs -> the one place HTTP calls to CvTailr.Api happen
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
- No project reference to `CvTailr.Api` or `CvTailr.Shared`.
- No hardcoded Api URLs in components — always via configuration and
  `ApiClient`.

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