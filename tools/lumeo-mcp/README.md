# @lumeo-ui/mcp-server

A [Model Context Protocol](https://modelcontextprotocol.io) server that
exposes the [Lumeo](https://github.com/Brain2k-0005/Lumeo) Blazor component
library to MCP-compatible LLM clients (Claude Desktop, Cursor, GitHub Copilot,
Zed, etc.).

Once installed, your LLM can look up real Lumeo parameters, slots, and usage
examples — so "build me a sign-in page with Lumeo" produces markup that
actually compiles.

## What it exposes

The server covers **the full Lumeo component catalog** from the generated
`registry.json` + `components-api.json`. Every component is returned with its
parameters (including which are `[EditorRequired]`), enums, sub-components, CSS
variables, examples, its **test-coverage tier**, and its **accessibility contract**
(roles, keyboard keys, focus) — the schema is source-derived (Roslyn) so it has full
coverage with no thin fallbacks, and stays in lockstep with each release.

### Tools

Components:
- `lumeo_list_components({ category?, query? })` — list all components (name, category, description)
- `lumeo_get_component({ name })` — full schema: every `[Parameter]` (with `required` for `[EditorRequired]`), enums, records, events, sub-components, CSS vars, source files, a curated example, and the component's **test-coverage tier**
- `lumeo_get_a11y({ name })` — accessibility contract: ARIA roles + `aria-*` attributes rendered, keyboard keys handled, focus management, and whether that behaviour is test-covered
- `lumeo_get_install({ name })` — install + setup: NuGet package, `using`s, DI, host includes, required parameters, portal/overlay requirements
- `lumeo_get_example({ name })` — a hand-curated Razor usage example
- `lumeo_search({ query })` — fuzzy search across components and services
- `lumeo_validate_markup({ markup })` — static-check Razor: unknown params, **type-bound enum-value validation** (e.g. `Size="Large"` is rejected), and missing-parent errors for sub-components that read a cascading context

Services, patterns, theme:
- `lumeo_list_services()` / `lumeo_get_service({ name })` — service-layer API (OverlayService, ThemeService, global enums, …)
- `lumeo_list_patterns()` / `lumeo_get_pattern({ name })` — higher-level composition patterns
- `lumeo_get_theme_tokens()` — design tokens ↔ CSS variables
- `lumeo_changelog()` — recent library changes

### Resources

- `lumeo://component/{Name}` — markdown reference per component
- `lumeo://category/{Name}` — overview of all components in a category

## Install

```bash
cd tools/lumeo-mcp
npm install
npm run build
```

This produces `dist/index.js`, a Node ESM entrypoint.

> Future: `npx -y @lumeo-ui/mcp-server` once published to npm.

## Configure your MCP client

### Claude Desktop

Edit `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
  "mcpServers": {
    "lumeo": {
      "command": "node",
      "args": ["/absolute/path/to/Lumeo/tools/lumeo-mcp/dist/index.js"]
    }
  }
}
```

Restart Claude Desktop. You should see the Lumeo tools/resources under the
connectors panel.

### Cursor

Add to `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "lumeo": {
      "command": "node",
      "args": ["/absolute/path/to/lumeo-mcp/dist/index.js"]
    }
  }
}
```

### VS Code (GitHub Copilot)

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "lumeo": {
      "type": "stdio",
      "command": "node",
      "args": ["/absolute/path/to/lumeo-mcp/dist/index.js"]
    }
  }
}
```

## Try it

After installing, ask your LLM:

> Build me a dashboard with three KpiCards using Lumeo.

or

> Show me how to build a sign-in page with Lumeo — email, password, and a submit button with validation.

The model will call `lumeo_search` + `lumeo_get_component` under the hood and
produce markup that uses the correct parameter names, two-way bindings, and
slot conventions.

## Development

```bash
npm run dev   # tsc --watch
npm start     # run the built server
```

The component schema is generated at build time, not hand-maintained:
`tools/Lumeo.RegistryGen` reads the actual Razor source via Roslyn and emits full
params / enums / events / sub-component metadata for every component into
`src/Lumeo/registry/`. `scripts/sync-registry.mjs` copies the generated
`registry.json` (164 components) and `components-api.json` here at `prebuild`
time, so the catalog never drifts from the source.

`src/components.ts` only layers a few extra hand-curated example snippets on top —
there is no thin/rich split and no manual catalog drift. To add a richer example
for a component, add an entry for it in `src/components.ts`; the merge layer
overlays it onto the generated schema.

## License

MIT
