# lumeo-mcp — tool reference

The `@lumeo-ui/mcp-server` MCP server exposes Lumeo's full API to agents. Its data is generated from the actual Razor source on every release, so it never drifts.

## Connecting it

```jsonc
// Claude Code (.mcp.json / settings) or Cursor / Copilot MCP config:
{
  "mcpServers": {
    "lumeo": { "command": "npx", "args": ["-y", "@lumeo-ui/mcp-server"] }
  }
}
```
On startup it logs (to stderr): `[lumeo-mcp] ready — N components, M categories, … theme tokens, … patterns, api vX.Y.Z`.

## Tools

### `lumeo_list_components` `{ category?, query? }`
List/filter all components. Returns `[{ name, category, subcategory, description, nugetPackage }]`. Categories: AI, Dashboard, Data Display, Drag & Drop, Feedback, Forms, Layout, Motion, Navigation, Overlay, Typography, Utility.

### `lumeo_search` `{ query }`
Fuzzy search across name/category/description, best matches first. E.g. `"modal"` → Dialog, Sheet, AlertDialog; `"chat message"` → AgentMessageList, …

### `lumeo_get_component` `{ name }`
**The big one.** Complete schema: every `[Parameter]` (name, type, default, XML-doc summary, `isCascading`/`captureUnmatched` flags), `enums` (name + values), `records` (name + signature), `events` (`EventCallback` properties), `subComponents` (each with its own full schema), `cssVars`, source `files`, and `examples` (`[{ title, code }]`). This is everything you need to write correct Razor without external docs. Call it before writing markup for any component you don't have memorised.

### `lumeo_get_example` `{ name }`
Just the working Razor snippet(s) — the exact code behind the docs-site demos plus any curated example. `{ component, docs, examples: [{ title, code }] }`.

### `lumeo_get_install` `{ name }`
Everything to actually run the component: `{ component, package, install: { dotnet, registryCli }, imports, di, hostIncludes, namespace, subComponents, requirements: [...], notes: [...], docs }`. `requirements` flags portal-component `<body>` theme classes, OverlayProvider needs, required parameters, etc.

### `lumeo_validate_markup` `{ markup }`
**Pre-flight check before you show the user Razor.** Pass the component/markup portion (it ignores `@code` blocks). Checks:
- components/sub-components exist
- parameter names are real (catches hallucinated APIs) — pass-through `class`/`style`/`data-*`/`aria-*` are allowed
- enum values are legal (e.g. `Variant="Button.ButtonVariant.Outlne"` → error)
- sub-components are nested inside their required parent (e.g. orphan `<DialogContent>` without `<Dialog>` → error)

Returns `{ ok, issues: [{ severity: "error"|"warning", component, message }], summary }`. Fix all errors, review warnings, re-run, then present.

### `lumeo_get_theme_tokens` `{}`
The 58 colour + radius tokens: `{ count, usage, tokens: [{ token, cssVar }] }`. Use as `bg-{token}`, `text-{token}`, `border-{token}`, `ring-{token}`; radii as `rounded-[var(--radius-…)]`. Never raw hex; never `dark:` prefixes.

### `lumeo_list_patterns` `{}` / `lumeo_get_pattern` `{ key }`
The 16 full-page "blocks" (dashboard, authentication, calendar, chat, ecommerce, file-manager, filters, form-wizard, kanban, mail, music, notifications, settings, social-feed, task-tracker, analytics). `list` → `[{ title, key, route, description }]`. `get` → `{ title, route, description, examples: [{ title, code }] }`. Great starting skeletons for a real page.

### `lumeo_changelog` `{}`
`{ version, apiSchemaGenerated, componentCount, changelog, note }` — which version's API surface this MCP reflects.

## Recommended workflow

1. **Find** — `lumeo_search` (or `lumeo_list_components` by category) to pick the component(s).
2. **Learn** — `lumeo_get_component` for the schema; `lumeo_get_example` for usage. For setup: `lumeo_get_install`. For a whole page: `lumeo_get_pattern`.
3. **Write** — Razor following the conventions (theme tokens, no `dark:`, SvgGlyph icons, sub-component nesting).
4. **Verify** — `lumeo_validate_markup` on what you wrote. Fix errors, re-run.
5. **Ship.**

## Resources (read-only)

- `lumeo://component/{name}` — markdown reference for one component
- `lumeo://category/{name}` — markdown overview of a category
