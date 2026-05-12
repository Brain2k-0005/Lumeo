/**
 * Static install/setup metadata that isn't derivable from the Razor source.
 * Keyed by NuGet package; plus a small list of component-specific gotchas
 * (portal components needing theme classes on <body>, etc.). Hand-maintained —
 * these change rarely. Surfaced via the `lumeo_get_install` MCP tool so an
 * LLM gets everything it needs to actually run a component, not just its API.
 */

export interface PackageSetup {
  /** NuGet package id. */
  package: string;
  /** `dotnet add package` line. */
  dotnetAdd: string;
  /** `lumeo add` is the registry CLI alternative for source-copy installs. */
  lumeoAddNote: string;
  /** `@using` lines to add to `_Imports.razor`. */
  usings: string[];
  /** Program.cs / DI registration, if any. */
  di: string[];
  /** Scripts/styles to include in the host page (`index.html` / `App.razor`). */
  hostIncludes: string[];
  /** Any extra prose. */
  notes: string[];
}

export const PACKAGE_SETUP: Record<string, PackageSetup> = {
  Lumeo: {
    package: "Lumeo",
    dotnetAdd: "dotnet add package Lumeo --prerelease",
    lumeoAddNote: "Or copy a single component into your project: `lumeo add <component>` (after `lumeo init`).",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();  // registers ComponentInteropService, OverlayService, ConsentService, etc."],
    hostIncludes: [
      `<link rel="stylesheet" href="_content/Lumeo/css/lumeo.css" />`,
      `<script src="_content/Lumeo/js/components.js"></script>`,
    ],
    notes: [
      "Tailwind v4: Lumeo ships pre-compiled utilities in lumeo.css — no Tailwind build step required for the components themselves.",
      "Dark mode is handled by CSS-variable swaps in lumeo.css. Do NOT use `dark:` Tailwind prefixes; toggle the `dark` class on <html>.",
      "All colours come from theme tokens (bg-primary, text-foreground, border-border, …) — never raw hex/hsl. Use the `lumeo_get_theme_tokens` tool for the full list.",
    ],
  },
  "Lumeo.Charts": {
    package: "Lumeo.Charts",
    dotnetAdd: "dotnet add package Lumeo.Charts --prerelease",
    lumeoAddNote: "`lumeo add chart` copies the Chart component + ECharts interop.",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoCharts();"],
    hostIncludes: [
      `<script src="_content/Lumeo.Charts/js/echarts.min.js"></script>`,
      `<script src="_content/Lumeo.Charts/js/chart-interop.js"></script>`,
    ],
    notes: ["Wraps Apache ECharts — 30+ chart types via the declarative <Chart> wrapper or raw EChartOption / OptionJson."],
  },
  "Lumeo.DataGrid": {
    package: "Lumeo.DataGrid",
    dotnetAdd: "dotnet add package Lumeo.DataGrid --prerelease",
    lumeoAddNote: "`lumeo add datagrid` copies the DataGrid + supporting types.",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoDataGrid();  // registers IDataGridExportService"],
    hostIncludes: [],
    notes: [
      "Excel/PDF export pulls in ClosedXML (MIT) and QuestPDF (dual-licensed — free under $1M revenue, otherwise paid). CSV/JSON export has no third-party dependency.",
      "In Blazor WebAssembly, hide PDF export (`ExportFormats=\"DataGridExportFormat.Csv | DataGridExportFormat.Excel\"`) — QuestPDF throws PlatformNotSupportedException there.",
    ],
  },
  "Lumeo.Editor": {
    package: "Lumeo.Editor",
    dotnetAdd: "dotnet add package Lumeo.Editor --prerelease",
    lumeoAddNote: "`lumeo add rich-text-editor`.",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoEditor();"],
    hostIncludes: [`<script src="_content/Lumeo.Editor/js/editor-interop.js"></script>`],
    notes: [],
  },
  "Lumeo.Scheduler": {
    package: "Lumeo.Scheduler",
    dotnetAdd: "dotnet add package Lumeo.Scheduler --prerelease",
    lumeoAddNote: "`lumeo add scheduler`.",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoScheduler();"],
    hostIncludes: [],
    notes: [],
  },
  "Lumeo.Gantt": {
    package: "Lumeo.Gantt",
    dotnetAdd: "dotnet add package Lumeo.Gantt --prerelease",
    lumeoAddNote: "`lumeo add gantt`.",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoGantt();"],
    hostIncludes: [],
    notes: [],
  },
  "Lumeo.Motion": {
    package: "Lumeo.Motion",
    dotnetAdd: "dotnet add package Lumeo.Motion --prerelease",
    lumeoAddNote: "`lumeo add <motion-component>` (e.g. `lumeo add border-beam`).",
    usings: ["@using Lumeo"],
    di: ["builder.Services.AddLumeo();", "builder.Services.AddLumeoMotion();"],
    hostIncludes: [`<script src="_content/Lumeo.Motion/js/motion-interop.js"></script>`],
    notes: ["Animation primitives (BorderBeam, Marquee, NumberTicker, Confetti, …). Most are pure CSS; a few use a small JS interop module."],
  },
};

/**
 * Components whose content renders into a portal/overlay outside the normal
 * component tree. They need `bg-background text-foreground` on <body> (or a
 * wrapper that is an ancestor of the portal root) or they render outside the
 * theme cascade and look unstyled.
 */
export const PORTAL_COMPONENTS = new Set([
  "Dialog", "Sheet", "Drawer", "Toast", "Popover", "Tooltip", "AlertDialog",
  "HoverCard", "ContextMenu", "DropdownMenu", "Command", "PopConfirm", "Tour",
  "DatePicker", "DateTimePicker", "TimePicker", "Combobox", "Cascader", "Mention", "Select",
]);

/** Components that need an OverlayProvider in the layout for the service-driven API. */
export const NEEDS_OVERLAY_PROVIDER = new Set(["Toast", "Dialog", "Sheet", "Drawer"]);

export function setupFor(nugetPackage: string): PackageSetup {
  return PACKAGE_SETUP[nugetPackage] ?? PACKAGE_SETUP.Lumeo;
}
