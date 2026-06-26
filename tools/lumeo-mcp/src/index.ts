#!/usr/bin/env node
/**
 * Lumeo MCP Server v2.0.1
 *
 * Source-of-truth schema for ALL Lumeo components, generated at build time
 * by `tools/Lumeo.RegistryGen` from the actual Razor source via Roslyn. Every
 * component now ships full parameter / enum / event / sub-component metadata —
 * no thin/rich split, no manual catalog drift.
 *
 *   Tools:
 *     - lumeo_list_components  — list/filter all components (name+category+description)
 *     - lumeo_get_component    — full schema (params, enums, events, sub-components, files, examples)
 *     - lumeo_search           — fuzzy search across name/category/description
 *     - lumeo_get_example      — working Razor snippet(s) for a component
 *     - lumeo_get_install      — NuGet + @using + DI + host includes + gotchas
 *     - lumeo_validate_markup  — pre-flight check Razor for hallucinated APIs / bad enums / bad nesting
 *     - lumeo_get_theme_tokens — the colour/radius CSS-variable tokens (the only legal colours)
 *     - lumeo_list_patterns / lumeo_get_pattern — full-page composed examples (dashboard, auth, …)
 *     - lumeo_changelog        — current version + schema generation timestamp + changelog link
 *
 *   Resources (URI template):
 *     - lumeo://component/{name}   — markdown reference per component
 *     - lumeo://category/{name}    — overview of all components in a category
 *
 * Transport: stdio (the standard for spawned MCP servers).
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ListResourceTemplatesRequestSchema,
  ListToolsRequestSchema,
  ReadResourceRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

import {
  loadComponentsApi,
  type ApiComponent,
  type ApiDocument,
  type ApiParameter,
  type ApiPattern,
  type ApiService,
  type ApiThemeToken,
} from "./componentsApi.js";
import { components as curatedExamples } from "./components.js";
import { loadRegistry, type TestCoverage } from "./registry.js";
import { setupFor, PORTAL_COMPONENTS, NEEDS_OVERLAY_PROVIDER } from "./installInfo.js";
import { createValidator } from "./validate.js";
import { readFileSync } from "node:fs";

const DOCS_BASE = "https://lumeo.nativ.sh";

// Server version, read from package.json so it can never drift from the published
// package — the publish job bumps package.json via `npm version`, and this file no
// longer carries a hand-edited literal that goes stale (it sat at 2.2.3 while the
// library was 3.19.0).
const SERVER_VERSION: string = (() => {
  try {
    const pkg = JSON.parse(readFileSync(new URL("../package.json", import.meta.url), "utf8"));
    return typeof pkg.version === "string" ? pkg.version : "0.0.0";
  } catch {
    return "0.0.0";
  }
})();

// ───────────────── Load source-of-truth schema ─────────────────

const api: ApiDocument = loadComponentsApi() ?? {
  version: "0.0.0",
  generated: "",
  stats: { componentCount: 0, totalParameters: 0, totalEnums: 0, totalRecords: 0, thinFallbacks: [] },
  components: {},
};

const components: ApiComponent[] = Object.values(api.components).sort((a, b) =>
  a.name.localeCompare(b.name),
);
const byName = new Map<string, ApiComponent>(
  components.map((c) => [c.name.toLowerCase(), c]),
);
// Sub-component → parent map. Sub-components (SheetContent, SelectTrigger,
// CardHeader, DialogContent, TabsTrigger, AccordionItem, …) are real,
// usable elements but only exist nested under their parent in the schema.
// Without this, an agent looking one up by name got "not found" even
// though the element exists. Resolving to the parent surfaces the full
// composition (the parent payload lists all its sub-components).
const bySubName = new Map<string, ApiComponent>();
for (const c of components) {
  for (const s of Object.values(c.subComponents)) {
    const key = s.componentName.toLowerCase();
    if (!byName.has(key) && !bySubName.has(key)) bySubName.set(key, c);
  }
}
// Service-layer public API (OverlayService, ThemeBuilder, IResponsiveService,
// global enums, …). These live in plain C#, not .razor, so they were
// previously invisible to agents. Indexed here so they're discoverable the
// same way components are.
const services: ApiService[] = (api.services ?? []).slice().sort((a, b) =>
  a.name.localeCompare(b.name),
);
const serviceByName = new Map<string, ApiService>(
  services.map((s) => [s.name.toLowerCase(), s]),
);

const CATEGORIES: string[] = Array.from(new Set(components.map((c) => c.category))).sort();
const themeTokens: ApiThemeToken[] = api.themeTokens ?? [];
const patterns: ApiPattern[] = api.patterns ?? [];
const patternByKey = new Map<string, ApiPattern>(
  patterns.map((p) => [p.title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, ""), p]),
);

// Hand-curated examples overlay (~30 components). Auto-gen schema wins for
// parameters/enums/events; the curated `example` Razor snippet is preserved
// as a documentation aid because LLMs benefit from seeing real usage.
const curatedExampleByName = new Map<string, string>(
  curatedExamples.map((c) => [c.name.toLowerCase(), c.example]),
);

// Per-component test-coverage (tier + which test dimensions exist), from registry.json.
// Surfaced in get_component so an agent can see, at a glance, what's battle-tested.
const coverageByName = new Map<string, TestCoverage>(
  (loadRegistry()?.components ?? [])
    .filter((c) => c.testCoverage)
    .map((c) => [c.name.toLowerCase(), c.testCoverage as TestCoverage]),
);

/** One-line human summary of a component's test coverage, e.g.
 *  "tier 3/4 · 32 tests · render, behavior, a11y, keyboard". */
function coverageSummary(tc: TestCoverage): string {
  const dims = [
    tc.render && "render",
    tc.behavior && "behavior",
    tc.a11y && "a11y",
    tc.keyboard && "keyboard",
    tc.scale && "scale",
    tc.e2e && "e2e",
  ].filter(Boolean) as string[];
  return `tier ${tc.tier}/4 · ${tc.tests} test${tc.tests === 1 ? "" : "s"}${dims.length ? ` · ${dims.join(", ")}` : ""}`;
}

// ───────────────── Helpers ─────────────────

function findComponent(name: string): ApiComponent | undefined {
  // Top-level match first; fall back to the parent of a matching sub-component
  // so e.g. "SheetContent" / "CardHeader" / "SelectTrigger" resolve.
  return byName.get(name.toLowerCase()) ?? bySubName.get(name.toLowerCase());
}

function score(c: ApiComponent, q: string): number {
  const needle = q.toLowerCase();
  if (!needle) return 0;
  let s = 0;
  if (c.name.toLowerCase() === needle) s += 100;
  if (c.name.toLowerCase().startsWith(needle)) s += 50;
  if (c.name.toLowerCase().includes(needle)) s += 25;
  if (c.category.toLowerCase().includes(needle)) s += 10;
  if (c.description.toLowerCase().includes(needle)) s += 5;
  // Sub-component name matches surface the parent — so searching a nested
  // element ("SheetContent", "TabsTrigger") finds the component that owns it.
  for (const sub of Object.values(c.subComponents)) {
    const sn = sub.componentName.toLowerCase();
    if (sn === needle) { s += 80; break; }
    if (sn.includes(needle)) { s += 20; break; }
  }
  return s;
}

function searchCatalog(query: string, category?: string): ApiComponent[] {
  let pool = components;
  if (category) pool = pool.filter((c) => c.category.toLowerCase() === category.toLowerCase());
  if (!query) return pool;
  return pool
    .map((c) => ({ c, s: score(c, query) }))
    .filter((x) => x.s > 0)
    .sort((a, b) => b.s - a.s)
    .map((x) => x.c);
}

function docsUrl(c: ApiComponent): string {
  // Convert PascalCase to kebab-case for the docs URL
  const slug = c.name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
  return `${DOCS_BASE}/components/${slug}`;
}

function paramRow(p: ApiParameter): string {
  const def = p.default ?? "—";
  const desc = p.description ?? "";
  const flags: string[] = [];
  if (p.isEditorRequired) flags.push("required");
  if (p.isCascading) flags.push("cascading");
  if (p.captureUnmatched) flags.push("captures unmatched");
  const flagStr = flags.length ? ` _(${flags.join(", ")})_` : "";
  return `| \`${p.name}\` | \`${p.type}\` | \`${def}\` | ${desc}${flagStr} |`;
}

function toComponentMarkdown(c: ApiComponent): string {
  const paramRows = c.parameters.map(paramRow).join("\n");
  const enumRows = c.enums
    .map((e) => `- **${e.name}**: ${e.values.join(", ")}${e.description ? ` — ${e.description}` : ""}`)
    .join("\n");
  const eventRows = c.events
    .map((e) => `- **${e.name}** \`${e.type}\`${e.description ? ` — ${e.description}` : ""}`)
    .join("\n");
  const subRows = Object.values(c.subComponents)
    .map((s) => `- **${s.componentName}** (${s.parameters.length} params)`)
    .join("\n");
  const filesBlock = c.files.length
    ? c.files.map((f) => `- \`${f}\``).join("\n")
    : "";
  const example = curatedExampleByName.get(c.name.toLowerCase());

  const coverage = coverageByName.get(c.name.toLowerCase());

  const sections = [
    `# ${c.name}`,
    "",
    `**Category:** ${c.category}${c.subcategory ? ` › ${c.subcategory}` : ""}`,
    `**NuGet:** \`${c.nugetPackage}\``,
    `**Namespace:** \`${c.namespace ?? "Lumeo"}\``,
    ...(coverage ? [`**Test coverage:** ${coverageSummary(coverage)}`] : []),
    "",
    c.description,
    "",
    "## Parameters",
    "",
    "| Name | Type | Default | Description |",
    "|---|---|---|---|",
    paramRows || "| _(none)_ | | | |",
    "",
  ];
  if (c.enums.length) sections.push("## Enums", "", enumRows, "");
  if (c.events.length) sections.push("## Events", "", eventRows, "");
  // Aggregate gotchas from the root component AND its sub-components — a
  // gotcha is often declared on a sub-component (e.g. SheetContent carries
  // the Sheet gotcha), so the root resource must surface those too.
  const gotchaLines = [
    ...(c.gotchas ?? []).map((g) => `- ${g}`),
    ...Object.values(c.subComponents).flatMap((s) =>
      (s.gotchas ?? []).map((g) => `- **${s.componentName}**: ${g}`)),
  ];
  if (gotchaLines.length) {
    sections.push("## Gotchas", "", gotchaLines.join("\n"), "");
  }
  if (Object.keys(c.subComponents).length) sections.push("## Sub-components", "", subRows, "");
  if (example) sections.push("## Example", "", "```razor", example, "```", "");
  if (filesBlock) sections.push("## Source files", "", filesBlock, "");
  sections.push(`_Docs: ${docsUrl(c)}_`);
  return sections.join("\n");
}

function toCategoryMarkdown(category: string): string {
  const inCat = components.filter((c) => c.category.toLowerCase() === category.toLowerCase());
  if (inCat.length === 0) return `# ${category}\n\nNo components in this category.`;
  const rows = inCat
    .map((c) => `| [\`${c.name}\`](lumeo://component/${c.name}) | ${c.description} |`)
    .join("\n");
  return [
    `# ${category}`,
    "",
    `${inCat.length} component${inCat.length === 1 ? "" : "s"}:`,
    "",
    `| Component | Description |`,
    `|---|---|`,
    rows,
    "",
  ].join("\n");
}

// ───────────────── Services ─────────────────

function findService(name: string): ApiService | undefined {
  return serviceByName.get(name.toLowerCase());
}

function toServiceMarkdown(s: ApiService): string {
  const propRows = s.properties
    .map((p) => `| \`${p.name}\` | \`${p.type}\` | \`${p.default ?? "—"}\` | ${p.summary ?? ""} |`)
    .join("\n");
  const methodRows = s.methods
    .map((m) => `- \`${m.signature}\` → \`${m.returnType}\`${m.summary ? ` — ${m.summary}` : ""}`)
    .join("\n");
  const enumRows = s.enumValues
    .map((e) => `- **${e.name}**${e.summary ? ` — ${e.summary}` : ""}`)
    .join("\n");
  const eventRows = s.events
    .map((e) => `- **${e.name}** \`${e.type}\`${e.summary ? ` — ${e.summary}` : ""}`)
    .join("\n");

  const sections = [
    `# ${s.name}`,
    "",
    `**Kind:** ${s.kind}`,
    `**Namespace:** \`${s.namespace ?? "Lumeo"}\``,
    "",
    s.summary ?? "_(no summary)_",
    "",
  ];
  if (s.enumValues.length) sections.push("## Values", "", enumRows, "");
  if (s.properties.length) {
    sections.push(
      "## Properties",
      "",
      "| Name | Type | Default | Summary |",
      "|---|---|---|---|",
      propRows,
      "",
    );
  }
  if (s.methods.length) sections.push("## Methods", "", methodRows, "");
  if (s.events.length) sections.push("## Events", "", eventRows, "");
  return sections.join("\n");
}

function toServiceListPayload(s: ApiService) {
  return { name: s.name, kind: s.kind, summary: s.summary };
}

function searchServices(query: string): ApiService[] {
  const needle = query.toLowerCase();
  if (!needle) return services;
  return services
    .map((s) => {
      let score = 0;
      if (s.name.toLowerCase() === needle) score += 100;
      if (s.name.toLowerCase().includes(needle)) score += 30;
      if ((s.summary ?? "").toLowerCase().includes(needle)) score += 5;
      if (s.enumValues.some((e) => e.name.toLowerCase() === needle)) score += 20;
      if (s.properties.some((p) => p.name.toLowerCase() === needle)) score += 10;
      if (s.methods.some((m) => m.name.toLowerCase() === needle)) score += 10;
      // Events too — so an event-only query (ViewportChanged, OnThemeChanged,
      // OnShow, OnClose) surfaces the owning service/interface.
      if (s.events.some((e) => e.name.toLowerCase() === needle)) score += 10;
      // Substring (fuzzy) member matching so partial queries discover the
      // service: "scrollable" → OverlayOptions.ScrollableBody, "showdialog" →
      // OverlayService.ShowDialogAsync, "mobile" → MobileFullscreen, etc.
      if (s.properties.some((p) => p.name.toLowerCase().includes(needle))) score += 6;
      if (s.methods.some((m) => m.name.toLowerCase().includes(needle))) score += 6;
      if (s.events.some((e) => e.name.toLowerCase().includes(needle))) score += 6;
      if (s.enumValues.some((e) => e.name.toLowerCase().includes(needle))) score += 6;
      return { s, score };
    })
    .filter((x) => x.score > 0)
    .sort((a, b) => b.score - a.score)
    .map((x) => x.s);
}

function toListPayload(c: ApiComponent) {
  return {
    name: c.name,
    category: c.category,
    subcategory: c.subcategory,
    description: c.description,
    nugetPackage: c.nugetPackage,
  };
}

function toGetPayload(c: ApiComponent) {
  // Build a rich JSON payload covering everything Claude Code needs to write
  // correct Razor without consulting external docs.
  const subComponents = Object.values(c.subComponents).map((s) => ({
    name: s.componentName,
    namespace: s.namespace,
    inheritsFrom: s.inheritsFrom,
    implements: s.implements,
    parameters: s.parameters,
    events: s.events,
    enums: s.enums,
    records: s.records,
    gotchas: s.gotchas ?? [],
  }));
  return {
    name: c.name,
    category: c.category,
    subcategory: c.subcategory,
    description: c.description,
    nugetPackage: c.nugetPackage,
    namespace: c.namespace,
    inheritsFrom: c.inheritsFrom,
    implements: c.implements,
    parameters: c.parameters,
    events: c.events,
    enums: c.enums,
    records: c.records,
    cssVars: c.cssVars,
    gotchas: c.gotchas ?? [],
    files: c.files,
    subComponents,
    examples: c.examples ?? [],
    curatedExample: curatedExampleByName.get(c.name.toLowerCase()) ?? null,
    testCoverage: coverageByName.get(c.name.toLowerCase()) ?? null,
    docs: docsUrl(c),
  };
}

// ───────────────── lumeo_get_a11y ─────────────────

function toA11yPayload(c: ApiComponent) {
  const a11y = c.a11y ?? { roles: [], ariaAttributes: [], keys: [], keyboardInteractive: false, focusManaged: false };
  const cov = coverageByName.get(c.name.toLowerCase());
  return {
    name: c.name,
    roles: a11y.roles,
    ariaAttributes: a11y.ariaAttributes,
    keyboardKeys: a11y.keys,
    keyboardInteractive: a11y.keyboardInteractive,
    focusManaged: a11y.focusManaged,
    // Whether the a11y / keyboard behaviour is actually covered by tests.
    tested: cov ? { a11y: cov.a11y, keyboard: cov.keyboard } : null,
    gotchas: c.gotchas ?? [],
    docs: docsUrl(c),
  };
}

// ───────────────── lumeo_get_install ─────────────────

function buildInstallInfo(c: ApiComponent) {
  const setup = setupFor(c.nugetPackage);
  const subNames = Object.values(c.subComponents).map((s) => s.componentName);
  const isPortal = PORTAL_COMPONENTS.has(c.name);
  const needsOverlayProvider = NEEDS_OVERLAY_PROVIDER.has(c.name);
  // [EditorRequired] params, from the real scanner flag (was a fragile description-text grep).
  const requiredParams = c.parameters
    .filter((p) => p.isEditorRequired)
    .map((p) => p.name);
  return {
    component: c.name,
    package: setup.package,
    install: {
      dotnet: setup.dotnetAdd,
      registryCli: setup.lumeoAddNote,
    },
    imports: setup.usings,
    di: setup.di,
    hostIncludes: setup.hostIncludes,
    namespace: c.namespace ?? "Lumeo",
    subComponents: subNames,
    requirements: [
      isPortal
        ? "Portal component: the page <body> (or an ancestor of the overlay root) needs the theme classes `bg-background text-foreground`, or this renders outside the theme cascade and looks unstyled."
        : null,
      needsOverlayProvider
        ? "For the service-driven API (e.g. ToastService / OverlayService), add an <OverlayProvider /> once in your layout."
        : null,
      requiredParams.length
        ? `Required parameters: ${requiredParams.map((p) => `\`${p}\``).join(", ")}.`
        : null,
      /Overlay/i.test(JSON.stringify(c.implements)) || isPortal
        ? "Overlay/dismiss patterns are handled internally (RegisterClickOutside / LockScroll / focus trap) — you don't wire those yourself."
        : null,
    ].filter(Boolean),
    notes: setup.notes,
    docs: docsUrl(c),
  };
}

// ───────────────── lumeo_validate_markup ─────────────────
// The validator lives in ./validate.ts so it can be unit-tested in isolation;
// build it from the loaded catalog here. Shared/global enums (Lumeo.Size,
// Orientation, …) live in the service layer rather than on any one component, so
// feed them in separately for type-bound enum validation.
const sharedEnums = services
  .filter((s) => s.kind === "enum")
  .map((s) => ({ name: s.name, values: s.enumValues.map((v) => v.name) }));
const validateMarkup = createValidator(components, sharedEnums);

// ───────────────── Server setup ─────────────────

const server = new Server(
  {
    name: "lumeo-mcp",
    version: SERVER_VERSION,
  },
  {
    capabilities: {
      tools: {},
      resources: {},
    },
  },
);

// ───── Tools ─────

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "lumeo_list_components",
      description:
        `List all ${components.length} Lumeo components, optionally filtered by category or query. ` +
        "Returns { name, category, subcategory, description, nugetPackage } per component. " +
        `Categories: ${CATEGORIES.join(", ")}.`,
      inputSchema: {
        type: "object",
        properties: {
          category: { type: "string", description: `Filter by category (${CATEGORIES.join(", ")}).` },
          query: { type: "string", description: "Free-text query matched against name, category, description." },
        },
      },
    },
    {
      name: "lumeo_get_component",
      description:
        "Get the COMPLETE schema for a Lumeo component: every [Parameter] " +
        "(name, type, default, XML doc summary), nested enums and records, " +
        "EventCallback events, sub-components (e.g. Dialog → DialogContent, " +
        "DialogHeader, DialogTrigger, ...), CSS variables, source files, and a " +
        "hand-curated Razor example when available. Sourced from the actual " +
        "Razor source via Roslyn — always in sync with the library.",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: {
            type: "string",
            description: "Component name (e.g. \"Button\", \"DataGrid\", \"Sheet\"). Case-insensitive.",
          },
        },
      },
    },
    {
      name: "lumeo_get_a11y",
      description:
        "Get the ACCESSIBILITY contract for a Lumeo component: the ARIA roles and aria-* " +
        "attributes it renders, the keyboard keys it handles, whether it manages focus, and " +
        "whether that a11y/keyboard behaviour is covered by tests. Use it to verify a " +
        "component's accessibility before relying on it, or to know which keys to exercise. " +
        "Extracted from the actual Razor source by RegistryGen.",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: {
            type: "string",
            description: "Component name (e.g. \"Dialog\", \"Tabs\", \"Select\"). Case-insensitive.",
          },
        },
      },
    },
    {
      name: "lumeo_list_services",
      description:
        `List all ${services.length} Lumeo SERVICE-LAYER public API types — services (OverlayService, ` +
        "ThemeService, ComponentInteropService), options records (OverlayOptions, AlertDialogOptions), " +
        "interfaces (IResponsiveService, IThemeService), the fluent ThemeBuilder, and global enums " +
        "(Size, Density, Side, Align, Orientation, Breakpoint, ThemeMode, …). These live in plain C#, " +
        "not Razor. Returns { name, kind, summary } per type. Use lumeo_get_service for the full API.",
      inputSchema: { type: "object", properties: {} },
    },
    {
      name: "lumeo_get_service",
      description:
        "Get the COMPLETE public API for a Lumeo service-layer type: its summary, public properties " +
        "(name, type, default, XML doc), public methods (signature, return type, doc), and enum values. " +
        "Covers OverlayService (ShowDialogAsync/ShowSheetAsync/ShowAlertDialogAsync), OverlayOptions " +
        "(ScrollableBody, MobileFullscreen, …), ThemeBuilder, IResponsiveService, AlertDialogOptions, and " +
        "the global enums. Sourced from the actual C# source via Roslyn.",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: {
            type: "string",
            description: "Service/type name (e.g. \"OverlayService\", \"OverlayOptions\", \"ThemeBuilder\", \"Size\"). Case-insensitive.",
          },
        },
      },
    },
    {
      name: "lumeo_search",
      description:
        `Fuzzy search across all ${components.length} Lumeo components (name, category, description) ` +
        `and ${services.length} service-layer types. Best matches first.`,
      inputSchema: {
        type: "object",
        required: ["query"],
        properties: {
          query: { type: "string", description: "Search terms (e.g. \"modal\", \"date\", \"chat message\")." },
        },
      },
    },
    {
      name: "lumeo_get_example",
      description:
        "Get working Razor example snippet(s) for a component — the exact code behind the docs-site demos. " +
        "Returns an array of { title, code }. Use this before writing markup for an unfamiliar component.",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: { type: "string", description: "Component name (e.g. \"Tabs\", \"DataGrid\"). Case-insensitive." },
        },
      },
    },
    {
      name: "lumeo_get_install",
      description:
        "Everything needed to actually USE a component: NuGet package + `dotnet add` line, `@using` imports, " +
        "DI registration (builder.Services.AddLumeo…), host-page <script>/<link> includes, sub-components, and " +
        "gotchas (portal components needing theme classes on <body>, OverlayProvider, required params).",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: { type: "string", description: "Component name. Case-insensitive." },
        },
      },
    },
    {
      name: "lumeo_validate_markup",
      description:
        "Validate a snippet of Razor that uses Lumeo components BEFORE compiling it. Checks: do the components exist? " +
        "are parameter names valid (catches hallucinated APIs)? are enum values legal? are sub-components nested inside " +
        "their required parent (e.g. <TabsContent> inside <Tabs>, <DialogContent> inside <Dialog>)? " +
        "Returns { ok, issues: [{ severity, component, message }] }. Pass-through HTML/data-/aria- attributes are allowed.",
      inputSchema: {
        type: "object",
        required: ["markup"],
        properties: {
          markup: { type: "string", description: "Razor markup to validate (the component/markup portion — @code blocks are ignored)." },
        },
      },
    },
    {
      name: "lumeo_get_theme_tokens",
      description:
        `List all ${themeTokens.length} Lumeo theme tokens (CSS custom properties driving colours + radii). ` +
        "These are the ONLY colours you should use — as Tailwind-style utilities like `bg-primary`, `text-foreground`, " +
        "`border-border`, `bg-card`, `text-muted-foreground`, `ring-ring`. Never raw hex/hsl, never `dark:` prefixes " +
        "(dark mode is a CSS-variable swap). Returns { token, cssVar } pairs.",
      inputSchema: { type: "object", properties: {} },
    },
    {
      name: "lumeo_list_patterns",
      description:
        `List all ${patterns.length} Lumeo "patterns" / "blocks" — full-page composed examples (dashboard, auth, chat, ` +
        "kanban, mail, settings, …) built entirely from Lumeo components. Returns { title, key, route, description }. " +
        "Use lumeo_get_pattern for the full Razor source of one.",
      inputSchema: { type: "object", properties: {} },
    },
    {
      name: "lumeo_get_pattern",
      description:
        "Get the complete Razor source of a Lumeo pattern/block (a full-page composed example). Returns title, " +
        "description, route, and the demo code snippet(s). Great as a starting skeleton for a real page.",
      inputSchema: {
        type: "object",
        required: ["key"],
        properties: {
          key: { type: "string", description: "Pattern key (kebab-case, from lumeo_list_patterns) — e.g. \"dashboard\", \"authentication\", \"chat\"." },
        },
      },
    },
    {
      name: "lumeo_changelog",
      description:
        "Current Lumeo package version, when the API schema was generated, and a link to the full changelog. " +
        "Use to check which version's API surface this MCP reflects.",
      inputSchema: { type: "object", properties: {} },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const { name, arguments: args } = req.params;
  const a = (args ?? {}) as Record<string, unknown>;

  switch (name) {
    case "lumeo_list_components": {
      const category = typeof a.category === "string" ? a.category : undefined;
      const query = typeof a.query === "string" ? a.query : "";
      const results = searchCatalog(query, category).map(toListPayload);
      return { content: [{ type: "text", text: JSON.stringify(results, null, 2) }] };
    }
    case "lumeo_get_component": {
      const wanted = typeof a.name === "string" ? a.name : "";
      const c = findComponent(wanted);
      if (!c) {
        return {
          isError: true,
          content: [{
            type: "text",
            text: `Component "${wanted}" not found. Use lumeo_list_components or lumeo_search to discover available components.`,
          }],
        };
      }
      return { content: [{ type: "text", text: JSON.stringify(toGetPayload(c), null, 2) }] };
    }
    case "lumeo_get_a11y": {
      const wanted = typeof a.name === "string" ? a.name : "";
      const c = findComponent(wanted);
      if (!c) {
        return {
          isError: true,
          content: [{
            type: "text",
            text: `Component "${wanted}" not found. Use lumeo_list_components or lumeo_search to discover available components.`,
          }],
        };
      }
      return { content: [{ type: "text", text: JSON.stringify(toA11yPayload(c), null, 2) }] };
    }
    case "lumeo_list_services": {
      const results = services.map(toServiceListPayload);
      return { content: [{ type: "text", text: JSON.stringify(results, null, 2) }] };
    }
    case "lumeo_get_service": {
      const wanted = typeof a.name === "string" ? a.name : "";
      const s = findService(wanted);
      if (!s) {
        return {
          isError: true,
          content: [{
            type: "text",
            text: `Service "${wanted}" not found. Use lumeo_list_services to discover available service-layer types.`,
          }],
        };
      }
      return { content: [{ type: "text", text: JSON.stringify(s, null, 2) }] };
    }
    case "lumeo_search": {
      const query = typeof a.query === "string" ? a.query : "";
      // Flat array (the original lumeo_search shape) so existing clients that
      // iterate the result list keep working. Component matches come first;
      // service matches are appended. Each item carries a `resultType`
      // discriminator ("component" | "service") for clients that want to
      // distinguish — a harmless extra field for those that don't.
      const results = [
        ...searchCatalog(query).map((c) => ({ resultType: "component", ...toListPayload(c) })),
        ...searchServices(query).map((s) => ({ resultType: "service", ...toServiceListPayload(s) })),
      ];
      return {
        content: [{
          type: "text",
          text: JSON.stringify(results, null, 2),
        }],
      };
    }
    case "lumeo_get_example": {
      const wanted = typeof a.name === "string" ? a.name : "";
      const c = findComponent(wanted);
      if (!c) {
        return { isError: true, content: [{ type: "text", text: `Component "${wanted}" not found. Use lumeo_search.` }] };
      }
      const examples = (c.examples ?? []).slice();
      const curated = curatedExampleByName.get(c.name.toLowerCase());
      if (curated && !examples.some((e) => e.code.trim() === curated.trim())) {
        examples.push({ title: `${c.name} (curated)`, code: curated });
      }
      if (examples.length === 0) {
        return { content: [{ type: "text", text: `No example on file for "${c.name}". See ${docsUrl(c)} or call lumeo_get_component for its full API.` }] };
      }
      return { content: [{ type: "text", text: JSON.stringify({ component: c.name, docs: docsUrl(c), examples }, null, 2) }] };
    }
    case "lumeo_get_install": {
      const wanted = typeof a.name === "string" ? a.name : "";
      const c = findComponent(wanted);
      if (!c) {
        return { isError: true, content: [{ type: "text", text: `Component "${wanted}" not found. Use lumeo_search.` }] };
      }
      return { content: [{ type: "text", text: JSON.stringify(buildInstallInfo(c), null, 2) }] };
    }
    case "lumeo_validate_markup": {
      const markup = typeof a.markup === "string" ? a.markup : "";
      if (!markup.trim()) {
        return { isError: true, content: [{ type: "text", text: "No markup provided." }] };
      }
      const result = validateMarkup(markup);
      const summary = result.ok
        ? (result.issues.length ? `OK with ${result.issues.length} warning(s).` : "OK — no issues found.")
        : `${result.issues.filter((i) => i.severity === "error").length} error(s), ${result.issues.filter((i) => i.severity === "warning").length} warning(s).`;
      return { content: [{ type: "text", text: JSON.stringify({ ...result, summary }, null, 2) }] };
    }
    case "lumeo_get_theme_tokens": {
      return { content: [{ type: "text", text: JSON.stringify({
        count: themeTokens.length,
        usage: "Use as Tailwind-style utilities: bg-{token}, text-{token}, border-{token}, ring-{token}, fill-{token}. e.g. `bg-primary text-primary-foreground`, `border-border/40`, `text-muted-foreground`. Radius tokens (radius, radius-sm, radius-lg, …) → `rounded-[var(--radius-lg)]`. Never raw hex; never `dark:` prefixes (dark mode swaps the variable values).",
        tokens: themeTokens,
      }, null, 2) }] };
    }
    case "lumeo_list_patterns": {
      const list = patterns.map((p) => ({
        title: p.title,
        key: p.title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, ""),
        route: p.route,
        description: p.description,
      }));
      return { content: [{ type: "text", text: JSON.stringify(list, null, 2) }] };
    }
    case "lumeo_get_pattern": {
      const key = (typeof a.key === "string" ? a.key : "").toLowerCase();
      const p = patternByKey.get(key) ?? patterns.find((x) => x.title.toLowerCase().includes(key) || x.route.includes(key));
      if (!p) {
        return { isError: true, content: [{ type: "text", text: `Pattern "${key}" not found. Use lumeo_list_patterns for valid keys.` }] };
      }
      return { content: [{ type: "text", text: JSON.stringify({
        title: p.title, route: `${DOCS_BASE}${p.route}`, description: p.description, examples: p.examples,
      }, null, 2) }] };
    }
    case "lumeo_changelog": {
      return { content: [{ type: "text", text: JSON.stringify({
        version: api.version,
        apiSchemaGenerated: api.generated,
        componentCount: api.stats.componentCount,
        changelog: `${DOCS_BASE}/docs/changelog`,
        note: "Detailed per-release notes live on the docs site. This MCP's API surface reflects the version above.",
      }, null, 2) }] };
    }
    default:
      return { isError: true, content: [{ type: "text", text: `Unknown tool: ${name}` }] };
  }
});

// ───── Resources ─────

server.setRequestHandler(ListResourcesRequestSchema, async () => ({
  resources: [
    ...components.map((c) => ({
      uri: `lumeo://component/${c.name}`,
      name: `${c.name} (Lumeo component)`,
      description: c.description,
      mimeType: "text/markdown",
    })),
    ...CATEGORIES.map((cat) => ({
      uri: `lumeo://category/${cat}`,
      name: `Lumeo ${cat} components`,
      description: `Overview of all Lumeo components in the ${cat} category.`,
      mimeType: "text/markdown",
    })),
    ...services.map((s) => ({
      uri: `lumeo://service/${s.name}`,
      name: `${s.name} (Lumeo ${s.kind})`,
      description: s.summary ?? `Lumeo service-layer ${s.kind}.`,
      mimeType: "text/markdown",
    })),
  ],
}));

server.setRequestHandler(ListResourceTemplatesRequestSchema, async () => ({
  resourceTemplates: [
    {
      uriTemplate: "lumeo://component/{name}",
      name: "Lumeo component reference",
      description: "Markdown reference for a single Lumeo component, generated from Razor source.",
      mimeType: "text/markdown",
    },
    {
      uriTemplate: "lumeo://category/{name}",
      name: "Lumeo category overview",
      description: "Markdown overview of all components in a Lumeo category.",
      mimeType: "text/markdown",
    },
    {
      uriTemplate: "lumeo://service/{name}",
      name: "Lumeo service-layer reference",
      description: "Markdown reference for a single Lumeo service-layer type, generated from C# source.",
      mimeType: "text/markdown",
    },
  ],
}));

server.setRequestHandler(ReadResourceRequestSchema, async (req) => {
  const uri = req.params.uri;
  const componentMatch = /^lumeo:\/\/component\/(.+)$/i.exec(uri);
  if (componentMatch) {
    const wanted = decodeURIComponent(componentMatch[1]!);
    const c = findComponent(wanted);
    if (!c) throw new Error(`Unknown Lumeo component: ${wanted}`);
    return { contents: [{ uri, mimeType: "text/markdown", text: toComponentMarkdown(c) }] };
  }
  const categoryMatch = /^lumeo:\/\/category\/(.+)$/i.exec(uri);
  if (categoryMatch) {
    const cat = decodeURIComponent(categoryMatch[1]!);
    return { contents: [{ uri, mimeType: "text/markdown", text: toCategoryMarkdown(cat) }] };
  }
  const serviceMatch = /^lumeo:\/\/service\/(.+)$/i.exec(uri);
  if (serviceMatch) {
    const wanted = decodeURIComponent(serviceMatch[1]!);
    const s = findService(wanted);
    if (!s) throw new Error(`Unknown Lumeo service: ${wanted}`);
    return { contents: [{ uri, mimeType: "text/markdown", text: toServiceMarkdown(s) }] };
  }
  throw new Error(`Unsupported resource URI: ${uri}`);
});

// ───── Start ─────

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  process.stderr.write(
    `[lumeo-mcp] ready — ${components.length} components, ${services.length} services, ${CATEGORIES.length} categories, ` +
    `${api.stats.totalParameters} params, ${api.stats.totalEnums} enums, ` +
    `${themeTokens.length} theme tokens, ${patterns.length} patterns, ` +
    `api v${api.version}, generated ${api.generated}\n`,
  );
}

main().catch((err) => {
  process.stderr.write(`[lumeo-mcp] fatal: ${err instanceof Error ? err.stack ?? err.message : String(err)}\n`);
  process.exit(1);
});
