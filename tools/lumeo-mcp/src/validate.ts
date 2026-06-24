// Markup validator for lumeo_validate_markup — extracted from index.ts so it can
// be unit-tested in isolation. Parameterized by the component catalog (rather than
// reaching into module-level state) so tests can drive it with a small synthetic
// catalog and the server can build it from the loaded components-api.

export interface ValidationIssue {
  severity: "error" | "warning";
  component?: string;
  message: string;
}

/** The minimal shape the validator needs from each parameter. The real ApiParameter
 *  (componentsApi.ts) structurally satisfies this — `type` binds the param to its enum,
 *  and `isCascading` marks a [CascadingParameter] (used to gate the parent-child rule). */
export interface ValidatorParam {
  name: string;
  type?: string;
  isCascading?: boolean;
}

/** The minimal shape the validator needs from each catalog component. The real
 *  ApiComponent (componentsApi.ts) structurally satisfies this. */
export interface ValidatorComponent {
  name: string;
  parameters: ValidatorParam[];
  enums: { name: string; values: string[] }[];
  subComponents: Record<
    string,
    { componentName: string; parameters: ValidatorParam[]; enums: { name: string; values: string[] }[] }
  >;
}

/** A shared/global enum (e.g. Lumeo.Size) defined outside any single component — passed
 *  in separately because such enums live in the service layer, not a component's `enums`. */
export interface SharedEnum {
  name: string;
  values: string[];
}

type ElementInfo = {
  params: Set<string>;
  /** param name → the bare enum name it is typed as (only when that enum is known). */
  paramEnum: Map<string, string>;
  parent?: string;
  /** True when this sub-component actually reads a CascadingValue from its parent
   *  (has a [CascadingParameter]). Plain presentational sub-components — e.g. a
   *  DialogHeader that is just a styled div — are false and don't require the parent. */
  parentCascades?: boolean;
};

/** Strips namespace / global:: / trailing nullable `?` to the bare type name:
 *  "Lumeo.Size?" → "Size", "global::Lumeo.Orientation" → "Orientation". */
function bareTypeName(type: string | undefined): string {
  if (!type) return "";
  return type.replace(/\?+$/, "").replace(/^global::/, "").split(".").pop() ?? "";
}

/** Global enum table: bare enum name (lowercased) → its display values. Built once from
 *  every component's `enums` plus the shared enums (Size, Orientation, …). The validator
 *  binds a param's declared `type` to one of these by name, so it checks a value against
 *  the param's ACTUAL enum rather than guessing from substring matches. */
function buildEnumTable(components: ValidatorComponent[], sharedEnums: SharedEnum[]): Map<string, string[]> {
  const table = new Map<string, string[]>();
  const add = (name: string, values: string[]) => {
    if (name && values.length && !table.has(name.toLowerCase())) table.set(name.toLowerCase(), values);
  };
  for (const e of sharedEnums) add(e.name, e.values);
  for (const c of components) {
    for (const e of c.enums) add(e.name, e.values);
    for (const s of Object.values(c.subComponents)) for (const e of s.enums) add(e.name, e.values);
  }
  return table;
}

function buildElementIndex(
  components: ValidatorComponent[],
  enumTable: Map<string, string[]>,
): Map<string, ElementInfo> {
  const m = new Map<string, ElementInfo>();
  // For each param, bind it to its enum by type (only when that enum is in the table).
  const bindEnums = (params: ValidatorParam[]) => {
    const paramEnum = new Map<string, string>();
    for (const p of params) {
      const bare = bareTypeName(p.type);
      if (bare && enumTable.has(bare.toLowerCase())) paramEnum.set(p.name, bare);
    }
    return paramEnum;
  };
  for (const c of components) {
    m.set(c.name.toLowerCase(), {
      params: new Set(c.parameters.map((p) => p.name)),
      paramEnum: bindEnums(c.parameters),
    });
    for (const s of Object.values(c.subComponents)) {
      m.set(s.componentName.toLowerCase(), {
        params: new Set(s.parameters.map((p) => p.name)),
        paramEnum: bindEnums(s.parameters),
        parent: c.name,
        parentCascades: s.parameters.some((p) => p.isCascading === true),
      });
    }
  }
  return m;
}

/** Builds a validateMarkup function bound to the given component catalog. `sharedEnums`
 *  supplies enums that live outside any component (e.g. Lumeo.Size) so type-bound enum
 *  validation works for shared types too. */
export function createValidator(components: ValidatorComponent[], sharedEnums: SharedEnum[] = []) {
  const enumTable = buildEnumTable(components, sharedEnums);
  const elementIndex = buildElementIndex(components, enumTable);

  return function validateMarkup(markup: string): { ok: boolean; issues: ValidationIssue[] } {
    const issues: ValidationIssue[] = [];
    // Find component tags: <Foo ...> or <Foo .../> or </Foo>. Lumeo components are PascalCase.
    const tagRx = /<\/?([A-Z][A-Za-z0-9]*)((?:\s+[^<>]*?)?)\/?>/g;
    // Track which known Lumeo components appear, to validate parent-child.
    const present = new Set<string>();
    let m: RegExpExecArray | null;
    while ((m = tagRx.exec(markup)) !== null) {
      const tag = m[1]!;
      const isClose = m[0].startsWith("</");
      const known = elementIndex.get(tag.toLowerCase());
      if (!known) continue; // not a Lumeo element (could be a user component / HTML — ignore)
      present.add(tag);
      if (isClose) continue;

      // Parse attributes (best-effort): Name="..." | Name='...' | Name="@expr" | Name=@expr | bare-name
      const attrBlob = m[2] ?? "";
      const attrRx = /([@A-Za-z_][\w-]*)\s*=\s*("(?:[^"]*)"|'(?:[^']*)'|@?[^\s"'<>]+)|([@A-Za-z_][\w-]*)(?=\s|$)/g;
      let am: RegExpExecArray | null;
      while ((am = attrRx.exec(attrBlob)) !== null) {
        let name = (am[1] ?? am[3] ?? "").trim();
        if (!name) continue;
        // Strip Blazor directive prefixes/suffixes: @bind-Foo, @bind-Foo:event, Foo:stopPropagation, @onclick, @attributes, @key, @ref, @bind
        if (name.startsWith("@bind-")) name = name.slice("@bind-".length).split(":")[0]!;
        else if (name.startsWith("@bind")) continue; // @bind / @bind:event on inputs — skip
        else if (name.startsWith("@on") || name === "@attributes" || name === "@key" || name === "@ref" || name === "@oninput" || name === "@onchange") continue;
        else if (name.startsWith("@")) continue; // other directives
        if (name.includes(":")) name = name.split(":")[0]!; // EventName:stopPropagation, :preventDefault
        if (name === "class" || name === "style" || name === "id") continue; // pass-through HTML attrs (Lumeo captures unmatched)
        if (/^data-|^aria-/i.test(name)) continue; // captured unmatched
        if (!known.params.has(name)) {
          // Could be a captured-unmatched HTML attr — only flag if it looks like a typo'd Lumeo param (PascalCase).
          if (/^[A-Z]/.test(name)) {
            issues.push({ severity: "warning", component: tag, message: `Unknown parameter \`${name}\` on <${tag}>. Did you mean one of: ${[...known.params].slice(0, 8).join(", ")}…? (Or it's a pass-through HTML attribute, which is allowed.)` });
          }
          continue;
        }
        // Enum value check: Foo="Bar.Baz.Qux" or Foo="Qux". Bind the param to its
        // declared enum TYPE (not a substring guess), then check the value against that
        // enum's allowed values. This catches Size="Large" (Large ∉ Size) and never
        // mis-attributes a value to an unrelated enum that merely shares a substring.
        const rawVal = (am[2] ?? "").replace(/^["']|["']$/g, "").trim();
        if (!rawVal || rawVal.startsWith("@")) continue; // dynamic expression — can't statically check
        const boundEnum = known.paramEnum.get(name);
        if (boundEnum) {
          const display = enumTable.get(boundEnum.toLowerCase())!;
          const lastSegRaw = rawVal.split(".").pop()!;
          const lastSeg = lastSegRaw.toLowerCase();
          if (!display.some((v) => v.toLowerCase() === lastSeg)) {
            issues.push({ severity: "error", component: tag, message: `\`${name}="${rawVal}"\` — \`${lastSegRaw}\` is not a valid ${boundEnum} value. Allowed: ${display.join(", ")}.` });
          }
        }
      }
    }

    // Parent-child: a sub-component that actually reads a CascadingValue from its parent
    // (parentCascades) must have that parent present. Purely presentational sub-components
    // — e.g. a DialogHeader that is just a styled div with no [CascadingParameter] — are
    // NOT flagged, so they can be used standalone without a false "must be used inside" error.
    for (const tag of present) {
      const known = elementIndex.get(tag.toLowerCase())!;
      if (known.parent && known.parentCascades && !present.has(known.parent)) {
        issues.push({ severity: "error", component: tag, message: `<${tag}> must be used inside <${known.parent}> (it reads a CascadingValue from it). No <${known.parent}> found in this markup.` });
      }
    }

    return { ok: !issues.some((i) => i.severity === "error"), issues };
  };
}
