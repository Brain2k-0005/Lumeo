#!/usr/bin/env node
// Copies the Lumeo registry.json from the repo into src/ so the TypeScript
// build can import it via `resolveJsonModule`. Runs as `prebuild`.
// If the source registry is missing (e.g. fresh clone, someone ran the MCP
// package install from outside the monorepo) we bail gracefully and leave
// whatever src/registry.json is already there (may be an older snapshot).

import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const src = resolve(here, "../../../src/Lumeo/registry/registry.json");
const dest = resolve(here, "../src/registry.json");

if (!existsSync(src)) {
  console.warn(`[lumeo-mcp] sync-registry: source not found at ${src} — keeping existing src/registry.json (if any).`);
  process.exit(0);
}

mkdirSync(dirname(dest), { recursive: true });
copyFileSync(src, dest);
console.log(`[lumeo-mcp] sync-registry: copied ${src} -> ${dest}`);
