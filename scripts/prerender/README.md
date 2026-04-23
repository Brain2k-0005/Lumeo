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
