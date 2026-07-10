---
name: lumeo
description: Use when building or editing a Blazor UI that uses the Lumeo component library (the `Lumeo` NuGet package and its satellites Lumeo.Charts / Lumeo.DataGrid / Lumeo.Editor / Lumeo.Scheduler / Lumeo.Gantt / Lumeo.Motion), or when the user mentions Lumeo components (Button, DataGrid, Sheet, Dialog, Tabs, DatePicker, Toast, …). Establishes how to look components up via the lumeo-mcp server and the non-negotiable conventions for writing correct Lumeo Razor.
---

# Lumeo

Lumeo is a Blazor component library for .NET 10 on Tailwind CSS v4 — 164 components (forms, data display, overlays, charts, DataGrid, AI primitives, motion) plus 16 full-page "block" patterns. shadcn-style API: composable sub-components, `CascadingValue` context, theme tokens.

## First move: use the lumeo-mcp server

If a `lumeo-mcp` MCP server is connected, **use it — don't guess at the API**. It generates its data from the actual Razor source on every release, so it never drifts. Tools:

| Tool | Use it to… |
|---|---|
| `lumeo_search` / `lumeo_list_components` | Find the right component for a need ("modal" → Dialog/Sheet, "date" → DatePicker/Calendar). |
| `lumeo_get_component` | Get the COMPLETE schema: every `[Parameter]` (name, type, default, doc), enums, records, events, sub-components, CSS vars, and example snippets. **Always do this before writing markup for a component you don't have memorised.** |
| `lumeo_get_example` | Working Razor snippet(s) — the exact code behind the docs-site demos. |
| `lumeo_get_install` | NuGet package + `dotnet add`, `@using` imports, `AddLumeo…()` DI registration, host-page `<script>/<link>` includes, sub-components, and gotchas (portal components needing theme classes on `<body>`, OverlayProvider, required params). |
| `lumeo_validate_markup` | **Pre-flight every Razor snippet you write.** Catches: components that don't exist, hallucinated parameter names, illegal enum values, sub-components not nested in their required parent. Run it before showing the user. |
| `lumeo_get_theme_tokens` | The 58 colour/radius tokens — the only legal colours. |
| `lumeo_list_patterns` / `lumeo_get_pattern` | Full-page composed examples (dashboard, auth, chat, kanban, mail, settings, …) — great starting skeletons. |
| `lumeo_changelog` | Which version's API this reflects. |

**Workflow:** `search` → `get_component` (+ `get_example`) → write Razor → `validate_markup` → fix any issues → done. For a new page, start from `get_pattern`. For setup questions, `get_install`.

If the MCP is **not** connected, fall back to [references/catalog.md](references/catalog.md) for the component list and tell the user that connecting `@lumeo-ui/mcp-server` (`npx -y @lumeo-ui/mcp-server`) gives you the full live API.

## Non-negotiable conventions

These are enforced project-wide. Violating them produces code that compiles but renders unstyled or breaks theming. Full detail in [references/conventions.md](references/conventions.md); the essentials:

1. **Colours are theme tokens, never raw hex/hsl.** Use Tailwind-style utilities: `bg-primary text-primary-foreground`, `bg-card`, `text-muted-foreground`, `border-border/40`, `ring-ring`. Get the full list with `lumeo_get_theme_tokens`. Radius: `rounded-[var(--radius-lg)]`.
2. **No `dark:` Tailwind prefixes.** Dark mode is a CSS-variable swap on the `dark` class on `<html>` — the tokens above just resolve to different values. Writing `dark:bg-x` is wrong.
3. **Icons: `<SvgGlyph Svg="@(Lucide.X)" />`** from the first-party `Lumeo.Icons.Lucide` pack (`@using Lumeo.Icons`), or `<Icon Name="X" />` for the built-in app-chrome vocabulary. Not raw inline SVG.
4. **JS interop goes through `ComponentInteropService`**, never `IJSRuntime` directly in a component.
5. **Two-way binding** is `Property` + `PropertyChanged` `EventCallback` pairs → `@bind-Property`.
6. **Sub-components read context via `CascadingValue`.** `<TabsContent>` must be inside `<Tabs>`, `<DialogContent>` inside `<Dialog>`, `<SelectItem>` inside `<Select>`, etc. `validate_markup` checks this.
7. **Portal/overlay components** (Dialog, Sheet, Drawer, Toast, Popover, Tooltip, AlertDialog, HoverCard, ContextMenu, DropdownMenu, Command, PopConfirm, Tour, DatePicker, Combobox, Select, …) render outside the normal tree — the page `<body>` (or an ancestor of the overlay root) needs `bg-background text-foreground` or they render outside the theme cascade and look unstyled. For the service-driven API (ToastService, OverlayService) add one `<OverlayProvider />` in the layout.
8. **Custom CSS:** every Lumeo component takes `Class` (appended to the component's own classes) and captures unmatched attributes — so `class`, `data-*`, `aria-*`, `style`, `id` pass straight through. Prefer `Class` for styling overrides.

## Install (quick)

```bash
dotnet add package Lumeo --prerelease          # core
dotnet add package Lumeo.DataGrid --prerelease # + satellites as needed
```
`_Imports.razor`: `@using Lumeo`
`Program.cs`: `builder.Services.AddLumeo();` (`+ AddLumeoDataGrid()` etc. for satellites)
Host page: `<link href="_content/Lumeo/css/lumeo.css" rel="stylesheet" />` + `<script src="_content/Lumeo/js/components.js"></script>`

The registry CLI is an alternative: `lumeo init` then `lumeo add <component>` copies a component's source into your project (shadcn-style). Use `lumeo_get_install` for the per-component specifics.

## References

- [references/conventions.md](references/conventions.md) — the full coding-conventions checklist
- [references/catalog.md](references/catalog.md) — all 164 components by category (offline fallback for when the MCP isn't connected)
- [references/mcp.md](references/mcp.md) — detailed lumeo-mcp tool reference + example calls
