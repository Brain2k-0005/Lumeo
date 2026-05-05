/**
 * Loads the auto-generated `components-api.json` produced by
 * `tools/Lumeo.RegistryGen` (Roslyn-based scan of every `[Parameter]` /
 * `[CascadingParameter]` property across every Razor component in the repo).
 *
 * This is the source-of-truth schema for ALL 131 Lumeo components. The
 * legacy hand-curated `components.ts` is kept as an OPTIONAL example overlay:
 * when it has an entry for a component we surface its `example` Razor snippet
 * verbatim alongside the auto-generated parameter list.
 */
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export interface ApiParameter {
  name: string;
  type: string;
  default: string | null;
  description: string | null;
  isCascading: boolean;
  captureUnmatched: boolean;
}

export interface ApiEvent {
  name: string;
  type: string;
  description: string | null;
}

export interface ApiEnum {
  name: string;
  values: string[];
  description: string | null;
}

export interface ApiRecord {
  name: string;
  signature: string;
  description: string | null;
}

export interface ApiSubComponent {
  componentName: string;
  fileName: string;
  namespace: string | null;
  inheritsFrom: string | null;
  implements: string[];
  parameters: ApiParameter[];
  events: ApiEvent[];
  enums: ApiEnum[];
  records: ApiRecord[];
  parseFailed: boolean;
  parseError: string | null;
}

export interface ApiComponent {
  name: string;
  category: string;
  subcategory: string | null;
  description: string;
  nugetPackage: string;
  files: string[];
  namespace: string | null;
  inheritsFrom: string | null;
  implements: string[];
  parameters: ApiParameter[];
  events: ApiEvent[];
  enums: ApiEnum[];
  records: ApiRecord[];
  cssVars: string[];
  subComponents: Record<string, ApiSubComponent>;
  parseFailed: boolean;
  parseError: string | null;
}

export interface ApiDocument {
  version: string;
  generated: string;
  stats: {
    componentCount: number;
    totalParameters: number;
    totalEnums: number;
    totalRecords: number;
    thinFallbacks: { name: string; reason: string }[];
  };
  components: Record<string, ApiComponent>;
}

function findPath(): string | null {
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    // dist/ -> ../src/components-api.json
    // src/  -> ./components-api.json
    const candidates = [
      resolve(here, "../src/components-api.json"),
      resolve(here, "./components-api.json"),
      // monorepo fallback when running uninstalled
      resolve(here, "../..", "components-api.json"),
    ];
    for (const c of candidates) if (existsSync(c)) return c;
  } catch { /* ignore */ }
  return null;
}

export function loadComponentsApi(): ApiDocument | null {
  const path = findPath();
  if (!path) return null;
  try {
    const raw = readFileSync(path, "utf8");
    const parsed = JSON.parse(raw) as ApiDocument;
    if (!parsed?.components) return null;
    return parsed;
  } catch {
    return null;
  }
}
