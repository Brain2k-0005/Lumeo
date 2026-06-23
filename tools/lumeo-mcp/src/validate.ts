// Markup validator for lumeo_validate_markup — extracted from index.ts so it can
// be unit-tested in isolation. Parameterized by the component catalog (rather than
// reaching into module-level state) so tests can drive it with a small synthetic
// catalog and the server can build it from the loaded components-api.

export interface ValidationIssue {
  severity: "error" | "warning";
  component?: string;
  message: string;
}

/** The minimal shape the validator needs from each catalog component. The real
 *  ApiComponent (componentsApi.ts) structurally satisfies this. */
export interface ValidatorComponent {
  name: string;
  parameters: { name: string }[];
  enums: { name: string; values: string[] }[];
  subComponents: Record<
    string,
    { componentName: string; parameters: { name: string }[]; enums: { name: string; values: string[] }[] }
  >;
}

type ElementInfo = { params: Set<string>; enums: Map<string, Set<string>>; parent?: string };

function buildElementIndex(components: ValidatorComponent[]): Map<string, ElementInfo> {
  const m = new Map<string, ElementInfo>();
  const enumValueSet = (vals: string[]) => new Set(vals.map((v) => v.toLowerCase()));
  for (const c of components) {
    const enums = new Map<string, Set<string>>();
    for (const e of c.enums) enums.set(e.name, enumValueSet(e.values));
    m.set(c.name.toLowerCase(), { params: new Set(c.parameters.map((p) => p.name)), enums });
    for (const s of Object.values(c.subComponents)) {
      const subEnums = new Map<string, Set<string>>();
      for (const e of s.enums) subEnums.set(e.name, enumValueSet(e.values));
      m.set(s.componentName.toLowerCase(), {
        params: new Set(s.parameters.map((p) => p.name)),
        enums: subEnums,
        parent: c.name,
      });
    }
  }
  return m;
}

/** Builds a validateMarkup function bound to the given component catalog. */
export function createValidator(components: ValidatorComponent[]) {
  const elementIndex = buildElementIndex(components);

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
        // Enum value check: Foo="Bar.Baz.Qux" or Foo="Qux"
        const rawVal = (am[2] ?? "").replace(/^["']|["']$/g, "").trim();
        if (!rawVal || rawVal.startsWith("@")) continue; // dynamic expression — can't statically check
        // Which enum does this param use? Match by param name heuristically against enum names.
        for (const [enumName, vals] of known.enums) {
          // crude: the param likely uses this enum if rawVal looks like EnumName.Value or matches a value
          const lastSeg = rawVal.split(".").pop()!.toLowerCase();
          const looksLikeThisEnum = rawVal.toLowerCase().includes(enumName.toLowerCase()) || vals.has(lastSeg);
          if (looksLikeThisEnum && !vals.has(lastSeg)) {
            issues.push({ severity: "error", component: tag, message: `\`${name}="${rawVal}"\` — \`${lastSeg}\` is not a valid ${enumName} value. Allowed: ${[...vals].join(", ")}.` });
          }
        }
      }
    }

    // Parent-child: every sub-component present should have its required parent present somewhere.
    for (const tag of present) {
      const known = elementIndex.get(tag.toLowerCase())!;
      if (known.parent && !present.has(known.parent)) {
        issues.push({ severity: "error", component: tag, message: `<${tag}> must be used inside <${known.parent}> (it reads a CascadingValue from it). No <${known.parent}> found in this markup.` });
      }
    }

    return { ok: !issues.some((i) => i.severity === "error"), issues };
  };
}
