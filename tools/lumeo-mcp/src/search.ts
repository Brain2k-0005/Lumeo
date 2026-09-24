// Component search — extracted from index.ts so it can be unit-tested in isolation
// (same rationale as validate.ts: parameterized by a minimal structural shape rather
// than reaching into module-level state).
//
// LU-15 (field report against 5.11.0): lumeo_search("flow diagram nodes edges") returned
// [] even though FlowCanvas's own description mentions "flow", "nodes" and "edges" — its
// description just never contains that exact FOUR-WORD substring. The old `score()` did
// `c.description.toLowerCase().includes(needle)` with `needle` = the WHOLE lowercased
// query string, so any multi-word query only matched a component whose name/category/
// description happened to contain that literal phrase verbatim. Real queries are natural
// -language phrases ("flow diagram nodes edges", "chat message bubble"), not exact
// substrings. Fixed by tokenizing the query on whitespace and scoring + summing each
// token independently — a component now surfaces if ANY of the query's words hit its
// name/category/description/sub-component names, ranked by how many (and how well) they
// hit, which is what "fuzzy search" in the tool description already promised.

/** The minimal shape search needs from each component. The real ApiComponent
 *  (componentsApi.ts) structurally satisfies this. */
export interface SearchableComponent {
  name: string;
  category: string;
  description: string;
  subComponents: Record<string, { componentName: string }>;
}

/** Splits a query into lowercase, non-empty whitespace-separated words. */
export function tokenize(query: string): string[] {
  return query
    .toLowerCase()
    .split(/\s+/)
    .map((t) => t.trim())
    .filter(Boolean);
}

/** Score a single component against a single (already-lowercased) query token. */
function scoreToken<T extends SearchableComponent>(c: T, needle: string): number {
  let s = 0;
  const name = c.name.toLowerCase();
  if (name === needle) s += 100;
  else if (name.startsWith(needle)) s += 50;
  else if (name.includes(needle)) s += 25;
  if (c.category.toLowerCase().includes(needle)) s += 10;
  if (c.description.toLowerCase().includes(needle)) s += 5;
  // Sub-component name matches surface the parent — so searching a nested
  // element ("SheetContent", "TabsTrigger") finds the component that owns it.
  for (const sub of Object.values(c.subComponents)) {
    const sn = sub.componentName.toLowerCase();
    if (sn === needle) { s += 80; break; }
    if (sn.includes(needle)) { s += 20; break; }
  }
  return s;
}

/** Score a component against a full (possibly multi-word) query: the sum of its
 *  per-token scores. A component needs to match only SOME of the query's words to
 *  surface — it does not have to contain the whole phrase verbatim. */
export function scoreComponent<T extends SearchableComponent>(c: T, query: string): number {
  const tokens = tokenize(query);
  if (tokens.length === 0) return 0;
  let total = 0;
  for (const t of tokens) total += scoreToken(c, t);
  return total;
}

export function searchComponents<T extends SearchableComponent>(
  components: readonly T[],
  query: string,
  category?: string,
): T[] {
  let pool = components;
  if (category) pool = pool.filter((c) => c.category.toLowerCase() === category.toLowerCase());
  if (!query) return pool.slice();
  return pool
    .map((c) => ({ c, s: scoreComponent(c, query) }))
    .filter((x) => x.s > 0)
    .sort((a, b) => b.s - a.s)
    .map((x) => x.c);
}
