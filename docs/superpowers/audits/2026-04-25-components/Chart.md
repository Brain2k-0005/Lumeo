# Chart

**Path:** `src/Lumeo/UI/Chart/`
**Class:** Other (data visualization)
**Files:** Chart.razor, ChartHelper.cs, ChartLabelHelper.cs, ChartPlaceholderFactory.cs, ChartSkeleton.razor, ChartSkeletonKind.cs, EChartOption.cs, Charts/AreaChart.razor, Charts/BarChart.razor, Charts/BoxPlotChart.razor, Charts/CalendarHeatmapChart.razor, Charts/CandlestickChart.razor, Charts/DonutChart.razor, Charts/EffectScatterChart.razor, Charts/FunnelChart.razor, Charts/GaugeChart.razor, Charts/GeoMapChart.razor, Charts/GraphChart.razor, Charts/HeatmapChart.razor, Charts/LineChart.razor, Charts/LiquidFillChart.razor, Charts/MixedChart.razor, Charts/NightingaleChart.razor, Charts/ParallelChart.razor, Charts/PictorialBarChart.razor, Charts/PieChart.razor, Charts/PolarBarChart.razor, Charts/RadarChart.razor, Charts/RadialChart.razor, Charts/SankeyChart.razor, Charts/ScatterChart.razor, Charts/SunburstChart.razor, Charts/ThemeRiverChart.razor, Charts/TreeChart.razor, Charts/TreemapChart.razor, Charts/WaterfallChart.razor, Charts/WordCloudChart.razor

## Contract — FAIL
- `Chart.razor` uses `@inject IJSRuntime JSRuntime` directly instead of `ComponentInteropService` (Chart.razor:26).
- Chart is the only component in the library that bypasses `ComponentInteropService` — justified by needing direct JS module import for ECharts, but violates the coding convention.

## API — OK
- Rich parameter set: `Option`, `OptionJson`, `Width`, `Height`, `Theme`, `Class`, `IsLoading`, etc.
- All wrapper charts (`BarChart`, `LineChart`, etc.) expose `Class`, `Width`, `Height`, `IsLoading`.

## Bugs — WARN
- `Chart.razor` uses `[Inject] IJSRuntime` directly (Dimension 3 — should use ComponentInteropService).
- `OnAfterRenderAsync` has JS interop calls outside `if (firstRender)` guard (lines 184–228, `else if (_initialized)` branch) — intentional update path, not a bug per se, but noted.
- `_ = InvokeAsync(...)` not used in lifecycle methods here — no finding.

## Docs — WARN
- Page: `docs/Lumeo.Docs/Pages/Components/ChartPage.razor` (exists)
- 0 ComponentDemo blocks (page uses direct chart-component markup with Links to sub-pages, no `<ComponentDemo>`)
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `chart`)
- Files declared: 37 of 37
- Missing from registry: none
- Component deps declared: OK (none required; ECharts loaded via CDN/interop)
