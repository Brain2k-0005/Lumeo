# Lumeo Package Split — DevExpress Model

**Status:** Approved 2026-04-25 — partially reversed 2026-04-30 (see footer)
**Trigger:** `Lumeo.2.0.0-rc.14.nupkg` is 918 KB — 92% of the 1 MB ceiling. Splitting is overdue and matches how DevExpress / Telerik / Syncfusion ship.

## Goal

Move from a single `Lumeo` package to a core + satellite layout. Heavy components live in opt-in satellite packages. Core stays under 700 KB; satellites are pay-per-use.

## Final package layout

| Package | Components | wwwroot assets | Est. size |
|---------|-----------|----------------|-----------|
| **Lumeo** (core) | Layout (Stack/Flex/Grid/Container/Center/Spacer/AspectRatio/Resizable/ScrollArea/Separator/Splitter), Typography (Text/Heading/Link/Code), Forms except RichTextEditor, Navigation, Overlay, Feedback, most Display, Motion, Theme, ConsentBanner, AI surface (initial home — may move) | `lumeo.css`, `theme.js`, `components.js` | ~600 KB |
| **Lumeo.Charts** | Chart + 30+ subtype components | `echarts-interop.js` | ~150 KB |
| **Lumeo.DataGrid** | DataGrid (28 files), DataTable, Filter | none extra | ~120 KB |
| **Lumeo.Editor** | RichTextEditor + (future) Mention extension + (future) Word import | `rich-text-editor.js` | ~80 KB |
| **Lumeo.Scheduler** | Scheduler | `scheduler.js` | ~50 KB |
| **Lumeo.Gantt** | Gantt | `gantt.js` | ~50 KB |

**`lumeo-utilities.css` (275 KB) is moved out** — not shipped in any NuGet package. Consumers either generate their own Tailwind utilities (recommended) or download `lumeo-utilities.css` separately from the registry CDN. This single change saves more than the entire DataGrid satellite.

**No metapackage.** `Lumeo` is core-only. If we add `Lumeo.All` later as convenience, fine — but not on day one. DevExpress / Microsoft.Extensions ship without one and it's the cleaner default.

## Versioning

**Lockstep.** All packages share one version, released together. Pattern: Microsoft.Extensions.*, DevExpress.Blazor.*. Consumers always know which versions go together. Repo's `Directory.Packages.props` carries the canonical version.

## Repo layout

```
src/
├── Lumeo/                        ← core (existing, slimmed)
├── Lumeo.Charts/                 ← new
│   ├── Lumeo.Charts.csproj
│   ├── UI/Chart/                 ← moved from src/Lumeo/UI/Chart/
│   ├── _Imports.razor
│   └── wwwroot/js/echarts-interop.js
├── Lumeo.DataGrid/               ← new
│   ├── Lumeo.DataGrid.csproj
│   └── UI/DataGrid/, UI/DataTable/, UI/Filter/
├── Lumeo.Editor/                 ← new
│   ├── Lumeo.Editor.csproj
│   ├── UI/RichTextEditor/
│   └── wwwroot/js/rich-text-editor.js
├── Lumeo.Scheduler/              ← new
├── Lumeo.Gantt/                  ← new
├── Lumeo.SourceGenerators/       ← unchanged
└── …
```

Each satellite `.csproj`:
- Targets `net10.0`
- Has `<ProjectReference Include="..\Lumeo\Lumeo.csproj" />` for shared types (ComponentInteropService, FormFieldContext, theme primitives)
- Has its own `<RootNamespace>Lumeo.Charts</RootNamespace>` etc. — but components stay in `@namespace Lumeo` so `<Chart />` works without extra `@using` for consumers
- Has its own `wwwroot/` so assets ship under `_content/Lumeo.Charts/js/...`

The pack output for satellites becomes a `PackageReference` to `Lumeo` at the same version. Lockstep version comes from a single `<Version>` property in `Directory.Build.props`.

## Registry / CLI changes

`tools/Lumeo.RegistryGen/Program.cs` extends each component entry with:

```json
"nugetPackage": "Lumeo"  // or "Lumeo.Charts", etc.
```

`tools/Lumeo.Cli/Commands.cs` `Add` flow:
1. Read registry entry for the requested component.
2. If `nugetPackage` ≠ "Lumeo" and isn't already in the consumer's `.csproj`:
   - Prompt: *"chart needs the `Lumeo.Charts` NuGet package. Install? [Y/n]"*
   - On accept, run `dotnet add package Lumeo.Charts`.
3. Continue with the existing copy-source flow.

## CI / publish

`.github/workflows/release.yml` extends:
- Build matrix: `[Lumeo, Lumeo.Charts, Lumeo.DataGrid, Lumeo.Editor, Lumeo.Scheduler, Lumeo.Gantt]`
- Pack each
- Push all to NuGet in one job, version sourced from `Directory.Build.props`
- Single release tag, single CHANGELOG entry, all packages move together

## Docs site impact

`docs/Lumeo.Docs/Lumeo.Docs.csproj` adds project references to all 6 satellites so demos can render Chart, DataGrid, Scheduler, etc. Zero code changes inside docs pages — the components still live in `@namespace Lumeo`.

## Migration impact (for consumers)

- 1.x → 2.0 was already a breaking-changes release. This adds: any consumer using `Chart`, `DataGrid`, `DataTable`, `Filter`, `RichTextEditor`, `Scheduler`, or `Gantt` must add the corresponding satellite package.
- Documented in `MIGRATION.md` under the existing 2.0 section. One-line additions per satellite.
- `lumeo update` command (if it exists) prompts to install missing satellites for components already vendored.

## Out of scope (for this work)

- The new `Lumeo.AI` satellite — keep the AI components in core for now. If the cluster grows or pulls heavy deps later, split it then.
- Source generators package split — `Lumeo.SourceGenerators` stays as-is.
- A `Lumeo.All` metapackage — defer until a consumer actually asks.
- The RichTextEditor feature additions (Mention extension + Word import) — these land in `Lumeo.Editor` after the split is in place, in a follow-up PR.

## Sequence of work

1. **Spec** (this) — committed.
2. **Diet pass** — strip `lumeo-utilities.css` from the package, move it to a CDN-only download; trim Chart wrappers if possible. Lands first, in case the split takes longer than planned.
3. **Project scaffolding** — create the 5 satellite csproj files, reference core, add to `Lumeo.slnx`.
4. **Component move** — move Chart/, DataGrid/, DataTable/, Filter/, RichTextEditor/, Scheduler/, Gantt/ folders from `src/Lumeo/UI/` to their satellite packages. Update any `@using` directives. Move corresponding `wwwroot/js/*.js` files.
5. **RegistryGen + CLI extension** — add `nugetPackage` field, update CLI prompt logic.
6. **CI/CD update** — release workflow handles 6 packages, lockstep version.
7. **Docs project references** — wire up so the docs site still builds.
8. **MIGRATION.md** — add satellite section under 2.0.
9. **Verify** — full build, full docs build, pack all six, sum sizes, confirm core ≤ 700 KB.

## Risks

- **Compiled Razor view overlap.** If a satellite's component references another satellite's component, we get a circular ref. **Mitigation:** core defines all shared abstractions; satellites only reference core, never each other. Cross-satellite refs are forbidden.
- **wwwroot path changes.** Existing JS interop calls reference `./_content/Lumeo/js/echarts-interop.js`. After the split, the path becomes `./_content/Lumeo.Charts/js/echarts-interop.js`. Every interop call in moved components needs the path updated.
- **Source generator visibility.** If `Lumeo.SourceGenerators` produces code referenced by satellite components, satellites need the generator wired up too. Verify before splitting.
- **NuGet propagation lag.** First publish of new package IDs takes a few hours to be searchable. Communicate in the release note.

---

## Update 2026-04-30 — `lumeo-utilities.css` reversal (rc.18)

**The decision in this spec to "move `lumeo-utilities.css` out — not shipped in any NuGet package" is reversed for rc.18 onward.** The file ships again in the `Lumeo` core package as `_content/Lumeo/css/lumeo-utilities.css`.

### Why the reversal
- The spec assumed a "registry CDN" would host the file separately. The CDN was never set up.
- README.md, `docs/Lumeo.Docs/Pages/Docs/Introduction.razor`, and `MIGRATION.md` were never updated to point to an alternate path. They still told consumers to load `_content/Lumeo/css/lumeo-utilities.css`.
- Result for ~6 weeks (rc.15 → rc.17): every consumer following the docs got a 404 + visibly unstyled Lumeo components (Badge `px-2.5` missing, DataGrid toolbar borders gone, Switch container gap collapsed, etc.). External feedback flagged it 2026-04-30.
- The 275 KB → ~245 KB pre-compiled bundle is still the right shape for a drop-in component library. Tailwind v4.2.2's improved minification + dedup means the regenerated file is now smaller than the rc.4–rc.14 bundle anyway.

### What changed in rc.18
1. Removed the `<Content Remove>` / `<None Pack="false">` block in `src/Lumeo/Lumeo.csproj` for `wwwroot/css/lumeo-utilities.css`. The Razor SDK's auto-glob picks it up as a static web asset.
2. Extended `src/Lumeo/Styles/lumeo-utilities.src.css` to `@source` all 6 satellite `UI/` folders so the bundle covers utility classes used inside DataGrid, Charts, Editor, Scheduler, Gantt, and Motion components — not just core.
3. `MIGRATION.md` was rewritten so the "no longer shipped" paragraph matches the rc.18 reality and explicitly tells rc.15–rc.17 users they can drop their workaround.

### What stayed
- The 6-package layout (Lumeo + Charts + DataGrid + Editor + Scheduler + Gantt) — that decision is correct and unchanged.
- Lockstep versioning via `Directory.Build.props`.
- The "advanced consumer" path: any consumer running their own Tailwind build can still skip `<link>`-ing the shipped file and emit utilities locally. That's just no longer the only path.

### Lesson
Don't decommission a publicly-documented asset until the replacement is shipped and the docs/onboarding flow points to it. The "deferred CDN" half of the original spec should have blocked the "stop shipping the file" half.

