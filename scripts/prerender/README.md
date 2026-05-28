# Lumeo docs prerender

Static HTML renderer for the Blazor WASM docs site. Produces `<route>/index.html`
for every route in `sitemap.xml` so first paint and search engine crawlers get
rendered content instead of an empty SPA shell.

## Run locally

```bash
# 1. Publish the docs site
dotnet publish ../../docs/Lumeo.Docs/Lumeo.Docs.csproj -c Release -o ../../release

# 2. Install prerender deps (one-time)
npm install

# 3. Crawl
node prerender.mjs ../../release/wwwroot

# 4. Optional: smoke-test a deployed URL
node verify.mjs https://lumeo.nativ.sh
```

## How it works

1. `server.mjs` serves `<publish-dir>/wwwroot/` on `localhost:4300`.
2. `prerender.mjs` reads `sitemap.xml`, launches headless Chromium, visits
   each route, waits for `document.documentElement.dataset.blazorReady === 'true'`
   (signalled from `MainLayout.razor`), then writes the rendered `<html>` to
   `<route>/index.html` in the publish dir.
3. Cloudflare Pages then serves the prerendered HTML; the Blazor WASM bundle
   hydrates on top. Identical markup = no visible flash.

## Performance notes

The crawler does **not** scroll, so `IntersectionObserver`-gated content never
becomes visible during prerender. Two patterns work around that so below-the-fold
or async content still ends up in the static HTML:

- **Eager-on-prerender flag.** `prerender.mjs` sets `window.__LUMEO_PRERENDER__`
  before any app script runs (`lumeo.isPrerender()` exposes it). Components that
  normally lazy-init via the observer (e.g. the landing-page constellation) check
  the flag and init eagerly during prerender, while keeping the lazy path for
  real users.
- **Inlined async data.** The `/components` catalog hydrates from an async
  `registry.json` fetch. The crawler inlines that JSON into the page as
  `<script id="lumeo-registry-data">`; `RegistryService` reads it synchronously on
  hydration, so the grid renders populated on the first pass (no skeleton swap,
  no layout shift). Falls back to fetching `/registry.json` on client-side nav.

### Known limitation — landing-page hero TBT

The landing hero renders an interactive MapLibre **globe** (`Lumeo.Maps`). Its
WebGL init dominates the home page's Total Blocking Time / Time-to-Interactive
and is above the fold, so `LazyRender` can't help (its observer fires
immediately). Deferring the mount to idle was measured to make it *worse*
(serialises the work after WASM boot). Meaningfully improving this needs either a
`Lumeo.Maps` rendering optimisation (a library change) or replacing the hero
globe with something lighter (a product decision) — neither belongs in a
docs-only change.
