/**
 * Loads the auto-generated `components-api.json` produced by
 * `tools/Lumeo.RegistryGen` (Roslyn-based scan of every `[Parameter]` /
 * `[CascadingParameter]` property across every Razor component in the repo).
 *
 * This is the source-of-truth schema for ALL Lumeo components. The
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
  /** True when the param carries [EditorRequired] — the consumer MUST supply it. */
  isEditorRequired?: boolean;
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
  gotchas?: string[];
  parseFailed: boolean;
  parseError: string | null;
}

export interface ApiExample {
  title: string;
  code: string;
}

/** Static a11y signals extracted from a component's markup by RegistryGen: the ARIA
 *  roles + aria-* attributes it renders, the keyboard keys it handles, and whether it
 *  manages focus. Surfaced via lumeo_get_a11y. */
export interface ApiA11y {
  roles: string[];
  ariaAttributes: string[];
  keys: string[];
  keyboardInteractive: boolean;
  focusManaged: boolean;
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
  gotchas?: string[];
  examples?: ApiExample[];
  subComponents: Record<string, ApiSubComponent>;
  a11y?: ApiA11y;
  parseFailed: boolean;
  parseError: string | null;
}

export interface ApiServiceProperty {
  name: string;
  type: string;
  default: string | null;
  summary: string | null;
}

export interface ApiServiceMethod {
  name: string;
  returnType: string;
  signature: string;
  summary: string | null;
}

export interface ApiServiceEnumValue {
  name: string;
  summary: string | null;
}

export interface ApiServiceEvent {
  name: string;
  type: string;
  summary: string | null;
}

/**
 * A public, consumer-facing service-layer type: a service class, options
 * record, interface, global enum, or static entry-point class. Mirrors the
 * shape emitted by `ServiceApiScanner` in `tools/Lumeo.RegistryGen`.
 */
export interface ApiService {
  name: string;
  kind: "class" | "record" | "interface" | "enum" | "staticClass";
  namespace: string | null;
  summary: string | null;
  properties: ApiServiceProperty[];
  methods: ApiServiceMethod[];
  events: ApiServiceEvent[];
  enumValues: ApiServiceEnumValue[];
}

export interface ApiThemeToken {
  token: string;
  cssVar: string;
}

export interface ApiPattern {
  title: string;
  route: string;
  description: string;
  examples: ApiExample[];
}

export interface ApiDocument {
  version: string;
  generated: string;
  stats: {
    componentCount: number;
    totalParameters: number;
    totalEnums: number;
    totalRecords: number;
    serviceCount?: number;
    thinFallbacks: { name: string; reason: string }[];
  };
  themeTokens?: ApiThemeToken[];
  patterns?: ApiPattern[];
  services?: ApiService[];
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
