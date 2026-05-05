#!/usr/bin/env node
/**
 * Lumeo MCP Server v2.0.0
 *
 * Source-of-truth schema for ALL 131 Lumeo components, generated at build time
 * by `tools/Lumeo.RegistryGen` from the actual Razor source via Roslyn. Every
 * component now ships full parameter / enum / event / sub-component metadata —
 * no thin/rich split, no manual catalog drift.
 *
 *   Tools:
 *     - lumeo_list_components  — list/filter all 131 components (name+category+description)
 *     - lumeo_get_component    — full schema (params, enums, events, sub-components, files)
 *     - lumeo_search           — fuzzy search across name/category/description
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
} from "./componentsApi.js";
import { components as curatedExamples } from "./components.js";

const DOCS_BASE = "https://lumeo.nativ.sh";

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
const CATEGORIES: string[] = Array.from(new Set(components.map((c) => c.category))).sort();

// Hand-curated examples overlay (~30 components). Auto-gen schema wins for
// parameters/enums/events; the curated `example` Razor snippet is preserved
// as a documentation aid because LLMs benefit from seeing real usage.
const curatedExampleByName = new Map<string, string>(
  curatedExamples.map((c) => [c.name.toLowerCase(), c.example]),
);

// ───────────────── Helpers ─────────────────

function findComponent(name: string): ApiComponent | undefined {
  return byName.get(name.toLowerCase());
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

  const sections = [
    `# ${c.name}`,
    "",
    `**Category:** ${c.category}${c.subcategory ? ` › ${c.subcategory}` : ""}`,
    `**NuGet:** \`${c.nugetPackage}\``,
    `**Namespace:** \`${c.namespace ?? "Lumeo"}\``,
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
    files: c.files,
    subComponents,
    example: curatedExampleByName.get(c.name.toLowerCase()) ?? null,
    docs: docsUrl(c),
  };
}

// ───────────────── Server setup ─────────────────

const server = new Server(
  {
    name: "lumeo-mcp",
    version: "2.0.0",
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
      name: "lumeo_search",
      description:
        `Fuzzy search across all ${components.length} Lumeo components (name, category, description). Best matches first.`,
      inputSchema: {
        type: "object",
        required: ["query"],
        properties: {
          query: { type: "string", description: "Search terms (e.g. \"modal\", \"date\", \"chat message\")." },
        },
      },
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
    case "lumeo_search": {
      const query = typeof a.query === "string" ? a.query : "";
      const results = searchCatalog(query).map(toListPayload);
      return { content: [{ type: "text", text: JSON.stringify(results, null, 2) }] };
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
  throw new Error(`Unsupported resource URI: ${uri}`);
});

// ───── Start ─────

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  process.stderr.write(
    `[lumeo-mcp] ready — ${components.length} components, ${CATEGORIES.length} categories, ` +
    `${api.stats.totalParameters} params, ${api.stats.totalEnums} enums, ` +
    `api v${api.version}, generated ${api.generated}\n`,
  );
}

main().catch((err) => {
  process.stderr.write(`[lumeo-mcp] fatal: ${err instanceof Error ? err.stack ?? err.message : String(err)}\n`);
  process.exit(1);
});
