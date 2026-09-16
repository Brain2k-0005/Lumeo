// Emits src/Lumeo/wwwroot/css/lumeo-classes.txt: every class-like token found in Lumeo's own
// component sources, one per line. It ships as a static web asset so that a consumer who runs
// their OWN Tailwind v4 build can point `@source` at it and have that build emit everything
// Lumeo's markup relies on - responsive variants included.
//
// Why this exists: Tailwind cannot scan a NuGet DLL, so a consumer's build only ever sees the
// consumer's markup. Loading lumeo-utilities.css next to that build is not a fix: both files
// put their rules in the same `utilities` layer, so whichever loads second wins for a class
// both contain. A consumer's plain `text-center` then overrides Lumeo's `sm:text-start`
// (media query or not), and Lumeo's dialog headers stay centred at every width. One build
// that sees both sets of markup is the only ordering that cannot go wrong.
//
// The filter is deliberately liberal, mirroring Tailwind's own heuristic scanner: a token the
// generator does not recognise is simply dropped by the consumer's build.
//
// CAVEAT — `@`-prefixed Tailwind utilities (e.g. the bare container-query `@container`
// utility, or a named `@container/name`) CANNOT be written directly as a literal class-
// attribute token in a .razor file's markup: Razor treats a bare `@` in an attribute value
// as a code transition, so the source has to read `@@container`, and this extractor's
// token regex (below) rejects anything starting with `@` — the doubled `@@container` is
// silently dropped and never reaches lumeo-classes.txt (nor, in practice, any Tailwind
// scan of the raw .razor source, whose own candidate matcher has the same blind spot for
// a leading `@` — verified: `container-type: inline-size` never actually applied when this
// was tried on DataGrid's scroll wrapper). Route an `@`-prefixed utility through a plain
// C# string instead (e.g. inside `Cx.Merge("@container/name ...", ...)`, as CardHeader.razor
// and FieldGroup.razor do) where Razor never sees the leading `@`, or avoid `@container`/
// `cqw` in library components entirely — CSS containment also makes that element the
// containing block for `position: fixed` descendants, which breaks any popover positioned
// in viewport coordinates underneath it (see DataGridHeaderCell / DataGrid's scroll wrapper
// history for the concrete regression this caused).
import { readFileSync, writeFileSync, readdirSync, statSync } from "node:fs";
import { join, extname } from "node:path";

const roots = [
  "src/Lumeo/UI", "src/Lumeo.DataGrid/UI", "src/Lumeo.Charts/UI", "src/Lumeo.Editor/UI",
  "src/Lumeo.Scheduler/UI", "src/Lumeo.Gantt/UI", "src/Lumeo.Motion/UI",
];
const exts = new Set([".razor", ".cs"]);
const files = [];
const walk = (d) => {
  for (const e of readdirSync(d)) {
    const p = join(d, e);
    if (statSync(p).isDirectory()) { if (!/^(bin|obj)$/.test(e)) walk(p); }
    else if (exts.has(extname(e))) files.push(p);
  }
};
for (const r of roots) { try { walk(r); } catch { /* satellite absent */ } }

// A candidate starts with a letter or an arbitrary-variant bracket; the rest is anything
// Tailwind could possibly parse. Anything else it drops.
const token = /^!?-?(?:[a-z]|\[)[^\s"`{}]*$/i;
const bare = /^(flex|grid|block|hidden|inline|contents|truncate|italic|underline|uppercase|lowercase|capitalize|invisible|visible|static|fixed|absolute|relative|sticky|border|rounded|shadow|ring|outline|resize|isolate|antialiased|container|sr-only|transition|transform|filter)$/;
const found = new Set();
for (const f of files) {
  // Per line, so a stray quote inside a doc comment cannot shift the pairing for the rest
  // of the file (it did: one quoted character in a <c> tag hid every class string below it).
  for (const line of readFileSync(f, "utf8").split(/\r?\n/)) {
    // Two passes. Pairing quotes finds plain attributes and C# strings; in
    // class="@Cx.Merge("w-max min-w-48", Class)" the class string sits between a closing and
    // an opening quote, so the second pass takes every literal that follows a "(" or "," as a
    // C# argument. (Scanning every segment between quotes instead drowned the manifest in
    // code tokens: 2200 candidates became 9600.)
    const literals = [...line.matchAll(/"([^"]*)"/g), ...line.matchAll(/[(,]\s*"([^"]*)"/g)];
    for (const m of literals) {
      for (const t of m[1].split(/\s+/)) {
        if (!t || t.includes("{") || t.includes("}") || t.length > 120) continue;
        if (!token.test(t)) continue;
        if (!/[-:\[]/.test(t) && !bare.test(t)) continue;
        found.add(t);
      }
    }
  }
}
const list = [...found].sort();
writeFileSync("src/Lumeo/wwwroot/css/lumeo-classes.txt", list.join("\n") + "\n");
console.log(`lumeo-classes.txt: ${list.length} candidates from ${files.length} files`);
