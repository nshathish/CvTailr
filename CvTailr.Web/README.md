# CvTailr.Web

Blazor Web App — the authoring UI for CVTailr. See `CLAUDE.md` for
architecture/conventions.

## Running locally

1. Start `CvTailr.Api` first — `CvTailr.Web/appsettings.json` points at
   `https://localhost:7077` (the Api's `https` launch profile).
2. From `CvTailr.Web/CvTailr.Web/`, run `dotnet run`.

## Styling (Tailwind CSS)

Styling uses Tailwind CSS v4 via its standalone CLI (`@tailwindcss/cli`),
not Bootstrap or a component library. It's wired up as a small,
Node-scoped build step — `CvTailr.Web/CvTailr.Web/package.json` — that
generates `wwwroot/css/app.css` (referenced by the host page) from the
Tailwind source at `Styles/app.css`.

`node_modules/` is gitignored; `wwwroot/css/app.css` is committed so the
app runs without a Node install, but must be regenerated after any
markup change that adds/removes utility classes.

From `CvTailr.Web/CvTailr.Web/`:

```
npm install        # one-time, or after package.json changes
npm run build:css  # one-off build of wwwroot/css/app.css
npm run watch:css  # rebuilds on save — run alongside `dotnet watch` during development
```

Tailwind's content scan is configured via `@source` directives in
`Styles/app.css` covering `Components/**/*.razor` and
`Components/**/*.razor.cs`, so utility classes used in markup or
code-behind aren't purged from the build.

`dotnet build`/`dotnet publish` also run `npm run build:css` automatically
via a `Target` in `CvTailr.Web.csproj` (skipped with a warning, not a
failure, if `node_modules` is missing — e.g. a Node-less CI/deploy
environment falls back to the committed `wwwroot/css/app.css`).
`dotnet watch` does **not** trigger it, though — keep `npm run watch:css`
running alongside for live rebuilds during development. See
`CvTailr.Web/docs/tailwind-css-wiring.md` for the full breakdown.
