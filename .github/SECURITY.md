# Security policy

## Supported versions

Lumeo is still pre-1.0 (actively tracking the `2.0.0-rc.*` line). Only the
latest release in each of the following lines receives security patches:

| Line          | Supported           |
| ------------- | ------------------- |
| `2.0.0-rc.*`  | ✅ latest rc only    |
| `1.x`         | ❌ end of life on 2.0 GA |

Once `2.0.0` ships stable, this table will be updated and only the currently-
supported minor line will get patches.

## Reporting a vulnerability

**Do not open a public GitHub issue for security problems.** Use GitHub's
private disclosure flow instead:

1. Go to <https://github.com/Brain2k-0005/Lumeo/security/advisories/new>
2. Fill out the form with as much detail as you can — affected components,
   Blazor hosting model (WASM / Server / SSR), reproduction steps, and any
   suggested remediation.

We'll acknowledge within **72 hours**, post a draft advisory within **7 days**,
and ship a patched release before publishing the advisory publicly.

## Scope

In scope:
  - XSS via unescaped `ChildContent` / `@bind` / `AdditionalAttributes`
  - CSRF in `Form` / `LumeoForm` binding
  - Clickjacking in overlay primitives (Dialog, Sheet, Drawer, Popover, DropdownMenu, Command, Tour)
  - Arbitrary file write via the `lumeo` CLI (`apply`, `add`)
  - Supply-chain issues in the `Lumeo`, `Lumeo.Cli`, or `Lumeo.Templates` NuGet packages
  - RCE / code execution via `@lumeo-ui/mcp-server` on npm
  - Any information disclosure from the preset API Worker at `api.lumeo.nativ.sh`

Out of scope (please don't report these — they're known / intentional):
  - Missing CSP headers on consumer apps (consumers control their own CSP)
  - Blazor WASM source code being readable in browser devtools (WASM is not secret storage)
  - Rate limits on `api.lumeo.nativ.sh/preset` (Cloudflare handles abuse at the edge)

Thanks for helping keep Lumeo safe.
