/**
 * Loads Lumeo's generated registry.json (synced into src/registry.json at
 * prebuild time — see `scripts/sync-registry.mjs`). All 125 components are
 * surfaced to the MCP server through this file so `lumeo_list_components`,
 * `lumeo_get_component`, and `lumeo_search` can cover the full catalog.
 *
 * Shape of the file:
 *   {
 *     "$schema": "...",
 *     "version": "...",
 *     "generated": "...",
 *     "components": {
 *       "<slug>": {
 *         "name": "ComponentName",
 *         "category": "Forms",
 *         "description": "...",
 *         "files": [...],
 *         "dependencies": [...],
 *         "cssVars": [...],
 *         "registryUrl": "https://lumeo.nativ.sh/registry/<slug>.json"
 *       }
 *     }
 *   }
 *
 * Failures are swallowed — the MCP server stays functional (just with the
 * hand-curated catalog only) when the sync step didn't run.
 */

import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

export interface RegistryComponent {
  /** Kebab-case slug key from the registry.json map (e.g. "data-grid"). */
  slug: string;
  /** PascalCase component name (e.g. "DataGrid"). */
  name: string;
  category: string;
  description: string;
  files: string[];
  dependencies: string[];
  cssVars: string[];
  registryUrl?: string;
}

export interface RegistryDocument {
  version: string;
  generated: string;
  components: RegistryComponent[];
}

interface RawEntry {
  name?: string;
  category?: string;
  description?: string;
  files?: string[];
  dependencies?: string[];
  cssVars?: string[];
  registryUrl?: string;
}

interface RawDocument {
  version?: string;
  generated?: string;
  components?: Record<string, RawEntry>;
}

function findRegistryPath(): string | null {
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    // dist/ → tools/lumeo-mcp; src/ → tools/lumeo-mcp
    const candidates = [
      resolve(here, "../src/registry.json"),
      resolve(here, "./registry.json"),
      // Fall back to the monorepo source if the sync step never ran.
      resolve(here, "../../..", "src/Lumeo/registry/registry.json"),
      resolve(here, "../..", "src/Lumeo/registry/registry.json"),
    ];
    for (const c of candidates) {
      if (existsSync(c)) return c;
    }
  } catch {
    // ignore
  }
  return null;
}

export function loadRegistry(): RegistryDocument | null {
  const path = findRegistryPath();
  if (!path) return null;
  try {
    const raw = readFileSync(path, "utf8");
    const parsed = JSON.parse(raw) as RawDocument;
    if (!parsed || typeof parsed !== "object" || !parsed.components) return null;

    const components: RegistryComponent[] = [];
    for (const [slug, entry] of Object.entries(parsed.components)) {
      if (!entry || !entry.name) continue;
      components.push({
        slug,
        name: entry.name,
        category: entry.category ?? "Unknown",
        description: entry.description ?? "",
        files: entry.files ?? [],
        dependencies: entry.dependencies ?? [],
        cssVars: entry.cssVars ?? [],
        registryUrl: entry.registryUrl,
      });
    }

    return {
      version: parsed.version ?? "unknown",
      generated: parsed.generated ?? "",
      components,
    };
  } catch {
    // swallow — fall back to the hand-curated catalog only
  }
  return null;
}
