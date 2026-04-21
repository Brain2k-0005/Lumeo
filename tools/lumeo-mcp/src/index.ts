#!/usr/bin/env node
/**
 * Lumeo MCP Server
 *
 * Exposes Lumeo's full 125-component catalog (sourced from the generated
 * registry.json, enriched by hand-curated rich entries for the top 35)
 * to MCP-compatible LLM clients (Claude Desktop, Cursor, Copilot, etc.):
 *
 *   Tools:
 *     - lumeo_list_components  — list/filter the full catalog
 *     - lumeo_get_component    — rich schema when curated, thin otherwise
 *     - lumeo_search           — fuzzy text search across all 125
 *
 *   Resources (URI template):
 *     - lumeo://component/{name}   — markdown reference per component
 *     - lumeo://category/{name}    — all components in a category
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
  catalog,
  CATEGORIES,
  registry,
  type CatalogEntry,
} from "./components.js";

const DOCS_BASE = "https://lumeo.nativ.sh";

// ───────────────── Helpers ─────────────────

const byName = new Map<string, CatalogEntry>(
  catalog.map((c) => [c.name.toLowerCase(), c]),
);

function findComponent(name: string): CatalogEntry | undefined {
  return byName.get(name.toLowerCase());
}

function score(c: CatalogEntry, q: string): number {
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

function searchCatalog(query: string, category?: string): CatalogEntry[] {
  let pool = catalog;
  if (category) {
    pool = pool.filter((c) => c.category.toLowerCase() === category.toLowerCase());
  }
  if (!query) return pool;
  return pool
    .map((c) => ({ c, s: score(c, query) }))
    .filter((x) => x.s > 0)
    .sort((a, b) => b.s - a.s)
    .map((x) => x.c);
}

function docsUrl(c: CatalogEntry): string {
  return `${DOCS_BASE}/components/${c.slug}`;
}

function toRichMarkdown(c: Extract<CatalogEntry, { thin: false }>): string {
  const paramRows = c.params
    .map((p) => `| \`${p.name}\` | \`${p.type}\` | \`${p.default}\` | ${p.description} |`)
    .join("\n");
  const slotRows = c.slots
    .map((s) => `| \`${s.name}\` | ${s.description} |`)
    .join("\n");
  const cssVarsBlock = c.cssVars.length
    ? `## CSS Variables\n\n${c.cssVars.map((v) => `- \`${v}\``).join("\n")}\n\n`
    : "";

  return [
    `# ${c.name}`,
    ``,
    `**Category:** ${c.category}`,
    ``,
    c.description,
    ``,
    `## Parameters`,
    ``,
    `| Param | Type | Default | Description |`,
    `|---|---|---|---|`,
    paramRows || `| _(none)_ | | | |`,
    ``,
    c.slots.length ? `## Slots\n\n| Slot | Description |\n|---|---|\n${slotRows}\n` : ``,
    `## Example`,
    ``,
    "```razor",
    c.example,
    "```",
    ``,
    cssVarsBlock,
    `_Docs: ${docsUrl(c)}_`,
  ].filter(Boolean).join("\n");
}

function toThinMarkdown(c: Extract<CatalogEntry, { thin: true }>): string {
  const filesBlock = c.files.length
    ? `## Files\n\n${c.files.map((f) => `- \`${f}\``).join("\n")}\n\n`
    : "";
  const cssVarsBlock = c.cssVars.length
    ? `## CSS Variables\n\n${c.cssVars.map((v) => `- \`${v}\``).join("\n")}\n\n`
    : "";
  const depsBlock = c.dependencies.length
    ? `## Dependencies\n\n${c.dependencies.map((d) => `- \`${d}\``).join("\n")}\n\n`
    : "";

  return [
    `# ${c.name}`,
    ``,
    `**Category:** ${c.category}`,
    ``,
    c.description,
    ``,
    `> Rich schema (parameters, slots, Razor example) is coming soon for this component.`,
    `> See the docs site for full usage: [${docsUrl(c)}](${docsUrl(c)})`,
    ``,
    filesBlock,
    depsBlock,
    cssVarsBlock,
  ].filter(Boolean).join("\n");
}

function toComponentMarkdown(c: CatalogEntry): string {
  return c.thin ? toThinMarkdown(c) : toRichMarkdown(c);
}

function toCategoryMarkdown(category: string): string {
  const inCat = catalog.filter((c) => c.category.toLowerCase() === category.toLowerCase());
  if (inCat.length === 0) {
    return `# ${category}\n\nNo components documented in this category yet.`;
  }
  const rows = inCat
    .map((c) => `| [\`${c.name}\`](lumeo://component/${c.name}) | ${c.thin ? "" : "*"}${c.description}${c.thin ? "" : "*"} |`)
    .join("\n");
  return [
    `# ${category}`,
    ``,
    `${inCat.length} component${inCat.length === 1 ? "" : "s"}:`,
    ``,
    `| Component | Description |`,
    `|---|---|`,
    rows,
    ``,
  ].join("\n");
}

function toListPayload(c: CatalogEntry) {
  return {
    name: c.name,
    category: c.category,
    description: c.description,
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
        `List all ${catalog.length} Lumeo components, optionally filtered by category or a free-text query. ` +
        "Returns an array of { name, category, description }. " +
        `Known categories: ${CATEGORIES.join(", ")}.`,
      inputSchema: {
        type: "object",
        properties: {
          category: {
            type: "string",
            description: `Filter by category (${CATEGORIES.join(", ")}).`,
          },
          query: {
            type: "string",
            description: "Free-text query matched against name, category, and description.",
          },
        },
      },
    },
    {
      name: "lumeo_get_component",
      description:
        "Get the reference for a single Lumeo component. " +
        "For the ~35 hand-curated components this returns full schema " +
        "(parameters, slots, Razor example, CSS variables). " +
        "For the remaining components, returns { name, category, description, files, cssVars, dependencies, note } with a link to the docs site.",
      inputSchema: {
        type: "object",
        required: ["name"],
        properties: {
          name: {
            type: "string",
            description: "Component name (e.g. \"Button\", \"DataGrid\"). Case-insensitive.",
          },
        },
      },
    },
    {
      name: "lumeo_search",
      description:
        `Fuzzy search across all ${catalog.length} Lumeo components (names, categories, descriptions). Returns best matches first.`,
      inputSchema: {
        type: "object",
        required: ["query"],
        properties: {
          query: {
            type: "string",
            description: "Search terms (e.g. \"modal\", \"date\", \"chat message\").",
          },
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
      return {
        content: [{ type: "text", text: JSON.stringify(results, null, 2) }],
      };
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
      const payload = c.thin
        ? {
            name: c.name,
            category: c.category,
            description: c.description,
            files: c.files,
            cssVars: c.cssVars,
            dependencies: c.dependencies,
            note: `Rich schema coming soon — see ${docsUrl(c)}`,
          }
        : {
            name: c.name,
            category: c.category,
            description: c.description,
            params: c.params,
            slots: c.slots,
            example: c.example,
            cssVars: c.cssVars,
            docs: docsUrl(c),
          };
      return {
        content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      };
    }

    case "lumeo_search": {
      const query = typeof a.query === "string" ? a.query : "";
      const results = searchCatalog(query).map(toListPayload);
      return {
        content: [{ type: "text", text: JSON.stringify(results, null, 2) }],
      };
    }

    default:
      return {
        isError: true,
        content: [{ type: "text", text: `Unknown tool: ${name}` }],
      };
  }
});

// ───── Resources ─────

server.setRequestHandler(ListResourcesRequestSchema, async () => ({
  resources: [
    ...catalog.map((c) => ({
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
      description:
        "Markdown reference for a single Lumeo component. Rich (params/slots/example) for curated components, thin (files/cssVars + docs link) otherwise.",
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
    const name = decodeURIComponent(componentMatch[1]!);
    const c = findComponent(name);
    if (!c) {
      throw new Error(`Unknown Lumeo component: ${name}`);
    }
    return {
      contents: [{
        uri,
        mimeType: "text/markdown",
        text: toComponentMarkdown(c),
      }],
    };
  }

  const categoryMatch = /^lumeo:\/\/category\/(.+)$/i.exec(uri);
  if (categoryMatch) {
    const cat = decodeURIComponent(categoryMatch[1]!);
    return {
      contents: [{
        uri,
        mimeType: "text/markdown",
        text: toCategoryMarkdown(cat),
      }],
    };
  }

  throw new Error(`Unsupported resource URI: ${uri}`);
});

// ───── Start ─────

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  // stderr is safe; stdout is the MCP transport
  const richCount = catalog.filter((c) => !c.thin).length;
  const thinCount = catalog.length - richCount;
  const registryNote = registry
    ? `, registry v${registry.version}`
    : " (no registry — curated-only mode)";
  process.stderr.write(
    `[lumeo-mcp] ready — ${catalog.length} components, ${CATEGORIES.length} categories ` +
    `(${richCount} rich, ${thinCount} thin)${registryNote}\n`,
  );
}

main().catch((err) => {
  process.stderr.write(`[lumeo-mcp] fatal: ${err instanceof Error ? err.stack ?? err.message : String(err)}\n`);
  process.exit(1);
});
