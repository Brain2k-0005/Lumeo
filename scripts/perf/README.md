# Phase 5 performance benchmarks

Playwright scripts that measure real numbers against the docs WASM app and
write JSON to `scripts/perf/results/`. The public write-up of these numbers
lives at `/docs/performance-facts` (`docs/Lumeo.Docs/Pages/Docs/PerformanceFacts.razor`).

## What each script measures

| Script | Metric | Drives |
| --- | --- | --- |
| `datagrid-100k.mjs` | Initial render, virtualized scroll fps, sort time, filter time | `/e2e/perf-bench` |
| `datagrid-hotpaths.mjs` | Column resize / column reorder, ms per pointer-move event | `/e2e/perf-bench` |
| `toast-burst.mjs` | Time for a toast burst to settle | `/e2e/perf-bench` |
| `wasm-boot.mjs` | WASM boot-to-interactive on the docs home | `/` |

`docs/Lumeo.Docs/Pages/E2E/PerfBench.razor` is a `noindex`, nav-less harness
page (same pattern as `Pages/E2E/P0Harness.razor`) with `data-testid` hooks
these scripts read. It is not meant for human eyes.

## Methodology

- **5 runs, median reported.** Every script opens a **fresh page** per run
  (`lib/util.mjs`'s `withFreshPage`) — no warm-up state carries over between
  runs.
- **Timing happens in-page.** Every duration is measured with
  `performance.now()` inside the browser, not by timing Playwright/CDP calls
  from Node — a CDP round trip per action would dwarf the actual work being
  measured, especially for `datagrid-hotpaths.mjs`'s per-move cost.
- **Hot-path measurement (`datagrid-hotpaths.mjs`).** Reuses the design
  documented in CHANGELOG.md 4.1.0 (PR #353): column resize/reorder do zero
  .NET interop calls per pointer-move. To measure that JS-only path in
  isolation, the script dispatches a tight in-page loop of synthetic
  `PointerEvent`s directly at the same elements
  `registerColumnResize`/`registerColumnReorder`
  (`src/Lumeo/wwwroot/js/components.js`) listen on, timing the whole loop and
  dividing by move count.
- **Not a lab.** This ran on one local Windows dev machine (specs recorded in
  every JSON's `machine` field), Chromium only (via `playwright`), a Release
  `dotnet run` dev-server (not a CDN-hosted, fully optimized `dotnet publish`
  deploy). Treat these as directionally honest, not lab-grade absolute
  numbers — see the `*Note` fields in each JSON for caveats specific to that
  metric (dataset size, quiet-window definition, etc.).

## Known findings baked into the methodology

Two things were discovered while building these scripts against this
project's docs app (a non-AOT-compiled, interpreter-tier Blazor WASM build)
and are disclosed rather than hidden:

1. **100k rows OOMs.** Materializing 100,000 `PerfRow` records plus
   DataGrid's client-side sort/search indices reliably exhausts the wasm32
   linear-memory ceiling — reproduced even after raising
   `WasmInitialHeapSize`/`EmccMaximumHeapSize` to ~3.5 GB / ~3.75 GB in
   `Lumeo.Docs.csproj` (right up against wasm32's 4 GB hard limit). Even 75k
   rows (the largest size that fit) took roughly a minute of wall clock for
   the first render alone. `datagrid-100k.mjs` therefore runs its automated,
   median-of-5 numbers against 10,000 rows instead, and reports the 75k/100k
   figures as one-off, hand-measured data points in its JSON output. A
   genuinely 100k+ row **client** dataset should use DataGrid's
   `Virtualized` + `OnRangeRequest` server-mode path
   (`/components/datagrid`), not an in-memory `List`.
2. **Toast bursts beyond `MaxToasts` crash the tab.** The docs site's global
   `ToastProvider` uses the default `MaxToasts=5`. Firing more than ~6-7
   `ToastService.Show()` calls in one synchronous burst reproducibly crashes
   the WASM renderer (Playwright reports `Target crashed`, not a catchable JS
   exception) via `MaxToasts`' oldest-eviction path. Confirmed by hand: 5
   toasts stable, 6 (one eviction) stable, 10 (five evictions) crashes.
   `toast-burst.mjs` therefore measures a 5-toast burst, and the crash itself
   is disclosed in the JSON's `toastCountNote` — a genuine 100-toast burst
   cannot currently be benchmarked until that bug is fixed.

## How to reproduce

`wasm-boot.mjs` measures the docs home's own boot cost, which real visitors
pay against the DEFAULT (auto-calculated, ~108 MB) heap — not the 512 MB
`LumeoPerfHeap` the other three scripts need for their in-memory datasets
(see the `WasmInitialHeapSize` comment in `Lumeo.Docs.csproj`). Because that
property is an MSBuild/publish-time setting, not something toggleable per
route at runtime, getting an accurate boot number means running it against a
**separate server session that does NOT set `LumeoPerfHeap`** — running it
against the perf-heap session would measure boot cost in an environment real
visitors never see.

```bash
# 1a. Boot cost: start the docs dev server with the DEFAULT heap (no
#     LumeoPerfHeap) — this is what real visitors get.
export DOTNET_ROLL_FORWARD=Major
cd docs/Lumeo.Docs
"$HOME/.dotnet/dotnet.exe" run --arch x64 -c Release --urls http://localhost:5287

# in another shell, install deps once
cd scripts/perf
npm install
node wasm-boot.mjs

# stop the server (Ctrl+C) before starting 1b.

# 1b. The other three benchmarks need the larger in-memory dataset headroom —
#     restart with LumeoPerfHeap=true.
cd docs/Lumeo.Docs
"$HOME/.dotnet/dotnet.exe" run --arch x64 -c Release --urls http://localhost:5287 -p:LumeoPerfHeap=true

# in another shell
cd scripts/perf
node datagrid-100k.mjs
node datagrid-hotpaths.mjs
node toast-burst.mjs
```

`run-all.mjs` runs all four sequentially against whichever single server is
currently up — convenient for a quick re-check of the dataset-heavy three,
but do not use it to reproduce the published boot number (it would run
`wasm-boot.mjs` against whatever heap that server session happens to have).

Override the base URL with `LUMEO_PERF_BASE_URL` if the docs server runs on a
different host/port. Results land in `scripts/perf/results/*.json` (gitignored
build artifacts aside — these JSON files ARE committed so the numbers on
`/docs/performance-facts` are traceable to a specific run).
