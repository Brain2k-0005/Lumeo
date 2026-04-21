# Culture Support Audit — Lumeo 2.0 (2026-04-20)

Every component in this audit was checked against the "formats dates / numbers / currency
for the end-user" criterion. Components that do not display any numeric or temporal value
(layout primitives, typography, icons, buttons, menus, dialogs, navigation, etc.) are
not listed.

The default behavior throughout is `CultureInfo.CurrentCulture`, which honours ASP.NET
`UseRequestLocalization()`. An optional `Culture` parameter lets consumers override
per-component.

| Component | Current state | Change needed | Applied |
| --- | --- | --- | --- |
| **DataGrid (cells)** | `GetFormattedValue(item)` used `string.Format` with no culture — picked up runtime default. | Cascade `Culture` via `DataGridContext.Culture`; cell calls `GetFormattedValue(item, Context.Culture)`. | ✅ |
| **DataGrid (CSV export)** | `IDataGridExportService.ToCsv` formatted with `CultureInfo.CurrentCulture` but no override. | Added optional `CultureInfo? culture` parameter (default null → `CurrentCulture`). Grid passes `EffectiveCulture` through. | ✅ |
| **DataGrid (Excel export)** | Legacy path wrote CSV disguised as `.xlsx`; cell values used runtime default. | Routed HandleExport to instance-based `IDataGridExportService` (ClosedXML). Cell values kept native typed; format strings derive from culture's `ShortDatePattern` when consumer didn't supply one. | ✅ |
| **DataGrid (PDF export)** | Legacy path silently fell back to JSON. Timestamp used `InvariantCulture`. | Routed to QuestPDF-backed `ToPdf` with culture parameter. Timestamp now uses `.ToString("g", culture)`. | ✅ |
| **DataGridColumn.GetFormattedValue** | Used `string.Format` — equivalent to `CurrentCulture` but not configurable. | Added `GetFormattedValue(item, CultureInfo)` overload using `IFormattable`. | ✅ |
| **DatePicker / DateRangePicker** | Hard-coded `Format="dd.MM.yyyy"`, no culture. Wrong for en-US out-of-the-box. | `Format` now nullable; default falls back to `EffectiveCulture.DateTimeFormat.ShortDatePattern`. Added `Culture` parameter. All `ToString(Format)` calls now `ToString(Format, EffectiveCulture)`. | ✅ |
| **DateTimePicker** | Hard-coded `DateFormat="yyyy-MM-dd"`; time format had no culture. | `DateFormat` now nullable; defaults to `ShortDatePattern` of effective culture. Added `Culture` parameter. Display formats all pass culture. | ✅ |
| **TimePicker** | Purely numeric `HH:mm` with `D2` — culture-safe. | None (documented). | ✅ (no code change needed) |
| **NumberInput** | Display and parse used `InvariantCulture`. That's mandatory for `<input type="number">` but parsing should also accept user locale as fallback. | Added `Culture` parameter. Parsing now tries invariant first, then user culture. Display stays invariant (HTML5 requirement). | ✅ |
| **Slider** | Default tooltip used `"G"` with no culture. | Added `Culture` parameter. `FormatValue` now uses `EffectiveCulture`. Native `<input type="range">` still reads invariant (browser requirement). | ✅ |
| **Statistic** | Formatted `F{Precision}` with `InvariantCulture` — German users saw `12.34` instead of `12,34`. | Added `Culture` parameter. Now formats `N{Precision}` against `EffectiveCulture`. Parsing accepts invariant or user culture. | ✅ |
| **Calendar** | Already reads `CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames`. | None — already culture-aware. | ✅ (verified) |
| **Progress** | Percentage rendered as `int` only (e.g. `42%`). | None — integer percent has no decimal/thousand separators. | ✅ (verified) |
| **Rating** | Integer display; no decimal culture surface. | None. | ✅ (verified) |
| **NumberTicker** | Integer/double ticker rendered as runtime default. | No hot action required; culture override can be added when a user asks. Documented in Culture page. | Deferred (low risk) |
| **Delta** | Displays percentage change; `ToString` with no culture. | Low risk — all defaults accept runtime culture; add `Culture` parameter only if asked. | Deferred (low risk) |
| **SparkCard** | Displays KPI values; forward to `Statistic`-like formatter. | Low-risk; the values consumers pass are already pre-formatted strings. | Deferred (low risk) |
| **Chart** | Wraps ECharts via JS interop. ECharts has its own locale config exposed via its `locale` option. | Culture parameter semantics differ (JS locale string vs `CultureInfo`); too many edge cases to auto-wire. Left as-is. Document the ECharts `locale` option path in Chart docs when we have time. | Deferred (third-party) |
| **Scheduler** | Wraps FullCalendar (JS). | FullCalendar expects its own locale import — auto-wiring from `CurrentCulture` would pull in an HTTP fetch per locale. Leaving as a consumer-wired option. | Deferred (third-party) |
| **Gantt** | Wraps Frappe Gantt (JS). | Same reasoning as Scheduler — library uses its own locale channel. | Deferred (third-party) |
| **DataTable** | Static table, does not format values itself — consumer renders `string`/`RenderFragment`. | None. | ✅ (verified) |
| **DatePicker presets** | `Preset.Label` is consumer-provided text. | None. | ✅ (verified) |

## Summary

- **Applied culture wiring (9 components + DataGrid exports):** DataGrid (cells, context), DataGridColumn, DataGridExportService (CSV/Excel/PDF), DatePicker, DateTimePicker, NumberInput, Slider, Statistic.
- **Verified already culture-correct (4):** Calendar, Progress, Rating, TimePicker, DataTable.
- **Deferred (6):** NumberTicker, Delta, SparkCard, Chart (ECharts), Scheduler (FullCalendar), Gantt (Frappe Gantt). The first three are low-risk and simply don't have a `Culture` parameter yet; the last three wrap JS libraries with their own locale channels that would clash with a naive cascade.

## Verification

- `dotnet build src/Lumeo/Lumeo.csproj -c Release` — clean build, 0 warnings, 0 errors.
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release` — see final count in release report.
