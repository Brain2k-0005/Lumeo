/**
 * Optional secondary source: Lumeo's generated registry.json.
 *
 * The registry is produced by the `Lumeo.RegistryGen` MSBuild tool
 * (at src/Lumeo/registry/registry.json). If it exists we load it as a
 * lightweight supplementary index — today that just means exposing the
 * full component name list so `lumeo_search` can surface components the
 * hand-curated catalog doesn't cover yet.
 *
 * Failures are swallowed — the MCP server stays fully functional using
 * only `components.ts` when the registry isn't present.
 */

import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

export interface RegistryComponent {
  name: string;
  category?: string;
  description?: string;
  file?: string;
}

export interface Registry {
  components: RegistryComponent[];
}

/**
 * Walk upward from this file to find the Lumeo repo root, then look for
 * the registry. We expect this server to live at `<repo>/tools/lumeo-mcp`.
 */
function findRegistryPath(): string | null {
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    // dist/ or src/ → tools/lumeo-mcp → tools → <repo root>
    const candidates = [
      resolve(here, "../../..", "src/Lumeo/registry/registry.json"),
      resolve(here, "../..", "src/Lumeo/registry/registry.json"),
      resolve(here, "..", "src/Lumeo/registry/registry.json"),
    ];
    for (const c of candidates) {
      if (existsSync(c)) return c;
    }
  } catch {
    // ignore
  }
  return null;
}

export function tryLoadRegistry(): Registry | null {
  const path = findRegistryPath();
  if (!path) return null;
  try {
    const raw = readFileSync(path, "utf8");
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed &&
      typeof parsed === "object" &&
      "components" in parsed &&
      Array.isArray((parsed as Registry).components)
    ) {
      return parsed as Registry;
    }
  } catch {
    // swallow — fall back to the hand-curated catalog
  }
  return null;
}
