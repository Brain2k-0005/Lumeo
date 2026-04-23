# Cloudflare Pages migration — `lumeo.nativ.sh`

Moves the docs site off GitHub Pages and onto Cloudflare Pages so we can:

- Ship **per-path `Cache-Control`** (already wired via `wwwroot/_headers`) — fixes the aggressive CSS caching we were fighting today and gives Blazor's `_framework/*.wasm` proper `immutable` caching for repeat visits.
- **HTTP/3, Brotli at edge, Early Hints** out of the box.
- Keep `api.lumeo.nativ.sh` (Worker, for preset-sharing) and the docs site under the same Cloudflare account — Worker → Pages **service bindings** are possible without crossing the public internet.
- Drop the GitHub Pages environment-protection friction (`v2.0-dev` can't deploy).

The existing `deploy-docs.yml` stays put during cutover. Nothing breaks if DNS
isn't switched yet.

---

## One-time Cloudflare setup (UI steps)

### 1. Create the Pages project

1. Dashboard → **Workers & Pages → Create → Pages → Direct Upload**.
2. Project name: `lumeo-docs` (must match `projectName` in `deploy-cloudflare.yml`).
3. Production branch: `master`.
4. Skip the build config — we upload pre-built output from CI. Click Create.

### 2. Disable Cloudflare features that break Blazor WASM

Under the project → **Settings → Functions / Build & Deployments / Speed**:

- **Auto Minify (JS/CSS/HTML): OFF** — Cloudflare's minifier rewrites IDs in
  dotnet.native.js and breaks the WASM boot.
- **Rocket Loader: OFF** — defers scripts, breaks Blazor bootstrap.
- **Email Obfuscation: OFF** — injects JS into every `<a href="mailto:...">`
  which fails the integrity check on fingerprinted HTML.
- **Browser Cache TTL: Respect Existing Headers** — so our `_headers` file wins.

### 3. Add the custom domain

Project → **Custom domains → Set up a domain** → `lumeo.nativ.sh`.

Cloudflare will show you the DNS record to add (a `CNAME` to
`lumeo-docs.pages.dev`). Add it in your DNS zone. Because the domain is on
Cloudflare DNS already, it's a one-click button.

Remove the old `A` records that pointed at GitHub Pages
(`185.199.108.153`, `185.199.109.153`, `185.199.110.153`, `185.199.111.153`)
once the Cloudflare Pages deployment is verified live.

### 4. Generate the deploy credentials

1. `dash.cloudflare.com/profile/api-tokens` → **Create Token**.
2. Start from the **"Edit Cloudflare Workers"** template.
3. Edit permissions and add **`Account → Cloudflare Pages → Edit`**.
4. Copy the token (shown once).

Repo → **Settings → Secrets and variables → Actions → New repository secret**:

- `CLOUDFLARE_API_TOKEN` — the token from step 4.
- `CLOUDFLARE_ACCOUNT_ID` — shown in the right sidebar on any Cloudflare dashboard page.

---

## First deploy

Either push anything to `master` or run the workflow manually:

```
gh workflow run "Deploy Docs to Cloudflare Pages"
```

The CF Pages project dashboard will show the deployment URL
(`https://<hash>.lumeo-docs.pages.dev`). Visit it before switching the custom
domain.

### Smoke tests after the first deploy

- [ ] `https://<preview>.lumeo-docs.pages.dev` — loads, no splash-screen hang.
- [ ] DevTools → Network — `dotnet.native.[hash].wasm` has
      `Cache-Control: public, max-age=31536000, immutable`.
- [ ] DevTools → Network — `css/tailwind.out.css?v=<sha>` matches the
      current commit's short SHA.
- [ ] Hard reload — no 404s, no console errors from `blazor.webassembly.js`.
- [ ] Navigate to `/components/button`, then reload — SPA fallback works.
- [ ] Dark mode toggle, customizer share button, all click targets.

## Cutover

Once the Pages deployment is verified:

1. Cloudflare dashboard → the `lumeo.nativ.sh` custom domain → **Activate**.
2. Wait ~5 min for DNS propagation + TLS provisioning.
3. Delete `.github/workflows/deploy-docs.yml` (the GitHub Pages workflow).
4. Commit the removal. Optionally remove the Pages environment in repo Settings.

## Rollback plan

If anything is weird:

1. Re-create the `A` records pointing at GitHub Pages (the four
   `185.199.*` IPs above).
2. Re-enable the old workflow by reverting the delete of `deploy-docs.yml`.

Total rollback time is one commit + one DNS propagation window.
