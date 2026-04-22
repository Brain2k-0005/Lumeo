# Lumeo preset API

Tiny Cloudflare Worker that stores/retrieves shareable theme preset configs. Free tier
handles way more traffic than a preset-share feature will ever need.

## One-time setup

All commands run from this `api/` directory.

### 1. Create a Cloudflare account

Go to https://dash.cloudflare.com/sign-up — email + password, no credit card.

### 2. Install wrangler

```bash
npm install -g wrangler
```

### 3. Authenticate

```bash
wrangler login
```

Opens your browser, asks to authorize the Wrangler app against your Cloudflare
account. Click Allow.

### 4. Create the KV namespace

```bash
wrangler kv namespace create PRESETS
```

Output will look like:

```
🌀 Creating namespace with title "lumeo-preset-PRESETS"
✨ Success!
Add the following to your configuration file:
[[kv_namespaces]]
binding = "PRESETS"
id = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"
```

Copy the `id` value and paste it into `wrangler.toml` — replace
`REPLACE_WITH_KV_NAMESPACE_ID` with the real id.

**Optional — preview namespace for local dev:**

```bash
wrangler kv namespace create PRESETS --preview
```

Paste its id into `wrangler.toml` as `preview_id` (uncomment the line).

### 5. Deploy

```bash
wrangler deploy
```

Output ends with something like:

```
Uploaded lumeo-preset (1.23 sec)
Deployed lumeo-preset triggers (0.45 sec)
  https://lumeo-preset.<your-subdomain>.workers.dev
```

Copy that URL. That's your preset API. Done.

### 6. Point the CLI + docs at it

Set the API base URL in two places:

**1. `docs/Lumeo.Docs/Shared/CustomizerSidebar.razor`** — the "Share preset" button POST target.
**2. `tools/Lumeo.Cli/ThemeCommands.cs`** — the CLI's fallback fetch endpoint.

Both read from a single constant `LumeoPresetApi.BaseUrl` in `src/Lumeo/Theming/LumeoPresetApi.cs`
(see that file). Update the default there and rebuild.

## Testing

### Quick ping

```bash
curl https://lumeo-preset.<subdomain>.workers.dev/
# → {"service":"lumeo-preset-api","ok":true}
```

### Store a preset

```bash
curl -X POST https://lumeo-preset.<subdomain>.workers.dev/preset \
  -H "Content-Type: application/json" \
  -d '{"theme":"blue","radius":"0.5","font":"inter"}'
# → {"id":"b4Ndd7"}
```

### Fetch it back

```bash
curl https://lumeo-preset.<subdomain>.workers.dev/preset/b4Ndd7
# → {"theme":"blue","radius":"0.5","font":"inter"}
```

## Custom domain (optional)

If you want `api.lumeo.nativ.sh/preset/<id>` instead of the `.workers.dev` URL:

1. Add the `lumeo.nativ.sh` zone in Cloudflare DNS
2. Create an A record for `api` pointing at `192.0.2.1` (placeholder — the Worker
   route overrides it, but Cloudflare needs a record to exist)
3. Uncomment the `routes = [...]` block in `wrangler.toml` and run `wrangler deploy`

## Updating the worker later

```bash
# Edit worker.js, then:
wrangler deploy
```

## Local development

```bash
wrangler dev
# Serves at http://localhost:8787
```

Uses the `preview_id` KV namespace if configured, otherwise a mock in-memory
store.

## Cost

Free tier covers:
- 100,000 requests/day
- 1,000 KV writes/day
- 100,000 KV reads/day
- 1 GB KV storage

A busy preset-share feature might do ~500 writes/day at peak. You will never
pay anything for this Worker unless Lumeo gets *significantly* more popular,
at which point paid tier is $5/month.

## Stopping / deleting

```bash
wrangler delete
```

Removes the Worker. The KV namespace survives — delete it with:

```bash
wrangler kv namespace delete --namespace-id=<id>
```
