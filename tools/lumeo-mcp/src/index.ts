#!/usr/bin/env node
/**
 * Lumeo MCP Server
 *
 * Exposes Lumeo's component catalog to MCP-compatible LLM clients
 * (Claude Desktop, Cursor, Copilot, etc.) as:
 *
 *   Tools:
 *     - lumeo_list_components  — list/filter the catalog
 *     - lumeo_get_component    — full reference for one component
 *     - lumeo_search           — fuzzy text search
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

import { components, CATEGORIES, type ComponentDoc } from "./components.js";
import { tryLoadRegistry } from "./registry.js";

// ───────────────── Helpers ─────────────────

const byName = new Map<string, ComponentDoc>(
  components.map(c => [c.name.toLowerCase(), c]),
);

const registry = tryLoadRegistry();

function findComponent(name: string): ComponentDoc | undefined {
  return byName.get(name.toLowerCase());
}

function score(c: ComponentDoc, q: string): number {
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

function searchComponents(query: string, category?: string): ComponentDoc[] {
  let pool = components;
  if (category) {
    pool = pool.filter(c => c.category.toLowerCase() === category.toLowerCase());
  }
  if (!query) return pool;
  return pool
    .map(c => ({ c, s: score(c, query) }))
    .filter(x => x.s > 0)
    .sort((a, b) => b.s - a.s)
    .map(x => x.c);
}

function toComponentMarkdown(c: ComponentDoc): string {
  const paramRows = c.params
    .map(p => `| \`${p.name}\` | \`${p.type}\` | \`${p.default}\` | ${p.description} |`)
    .join("\n");
  const slotRows = c.slots
    .map(s => `| \`${s.name}\` | ${s.description} |`)
    .join("\n");
  const cssVarsBlock = c.cssVars.length
    ? `## CSS Variables\n\n${c.cssVars.map(v => `- \`${v}\``).join("\n")}\n\n`
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
  ].filter(Boolean).join("\n");
}

function toCategoryMarkdown(category: string): string {
  const inCat = components.filter(c => c.category.toLowerCase() === category.toLowerCase());
  if (inCat.length === 0) {
    return `# ${category}\n\nNo components documented in this category yet.`;
  }
  const rows = inCat
    .map(c => `| [\`${c.name}\`](lumeo://component/${c.name}) | ${c.description} |`)
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
        "List Lumeo components, optionally filtered by category or a free-text query. " +
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
        "Get the full reference for a single Lumeo component: parameters, slots, a ready-to-use Razor example, and the CSS variables it consumes.",
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
        "Fuzzy search across component names, categories, and descriptions. Returns best matches first.",
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
      const results = searchComponents(query, category).map(c => ({
        name: c.name,
        category: c.category,
        description: c.description,
      }));
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
      const payload = {
        name: c.name,
        category: c.category,
        description: c.description,
        params: c.params,
        slots: c.slots,
        example: c.example,
        cssVars: c.cssVars,
      };
      return {
        content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      };
    }

    case "lumeo_search": {
      const query = typeof a.query === "string" ? a.query : "";
      const results = searchComponents(query).map(c => ({
        name: c.name,
        category: c.category,
        description: c.description,
      }));
      // Also surface registry-only components, if the registry exists and
      // the query hits something we don't have curated docs for yet.
      if (registry && query) {
        const q = query.toLowerCase();
        const extras = registry.components
          .filter(r => r.name && !byName.has(r.name.toLowerCase()))
          .filter(r =>
            r.name.toLowerCase().includes(q) ||
            (r.description ?? "").toLowerCase().includes(q) ||
            (r.category ?? "").toLowerCase().includes(q),
          )
          .slice(0, 20)
          .map(r => ({
            name: r.name,
            category: r.category ?? "Unknown",
            description: r.description ?? "(from registry — no curated docs yet)",
          }));
        results.push(...extras);
      }
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
    ...components.map(c => ({
      uri: `lumeo://component/${c.name}`,
      name: `${c.name} (Lumeo component)`,
      description: c.description,
      mimeType: "text/markdown",
    })),
    ...CATEGORIES.map(cat => ({
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
      description: "Markdown reference (parameters, slots, example) for a single Lumeo component.",
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
  process.stderr.write(
    `[lumeo-mcp] ready — ${components.length} components, ${CATEGORIES.length} categories${registry ? `, registry (${registry.components.length} entries) loaded` : ""}\n`,
  );
}

main().catch((err) => {
  process.stderr.write(`[lumeo-mcp] fatal: ${err instanceof Error ? err.stack ?? err.message : String(err)}\n`);
  process.exit(1);
});
