// One-shot: regenerate references/catalog.md from the components-api.json the
// lumeo-mcp ships. Run from the repo root: `node skills/lumeo/gen-catalog.mjs`.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const apiPath = resolve(here, "../../tools/lumeo-mcp/src/components-api.json");
const a = JSON.parse(readFileSync(apiPath, "utf8"));

const byCat = {};
for (const c of Object.values(a.components)) (byCat[c.category] ??= []).push(c);

let out = `# Lumeo component catalog

All ${Object.keys(a.components).length} components by category, plus ${a.patterns.length} full-page patterns and the ${a.themeTokens.length} theme tokens. Generated from \`components-api.json\` (\`node skills/lumeo/gen-catalog.mjs\`).

> This is the **offline fallback**. When the \`lumeo-mcp\` server is connected, prefer \`lumeo_search\` / \`lumeo_get_component\` / \`lumeo_get_example\` — they give the live, complete per-parameter API.

Satellite packages: a component tagged **[Charts]** needs \`Lumeo.Charts\`, **[DataGrid]** \`Lumeo.DataGrid\`, **[Editor]** \`Lumeo.Editor\`, **[Scheduler]** \`Lumeo.Scheduler\`, **[Gantt]** \`Lumeo.Gantt\`, **[Motion]** \`Lumeo.Motion\`. Everything else is in core \`Lumeo\`.
`;

for (const cat of Object.keys(byCat).sort()) {
  out += `\n## ${cat}\n\n`;
  for (const c of byCat[cat].sort((x, y) => x.name.localeCompare(y.name))) {
    const tag = c.nugetPackage === "Lumeo" ? "" : ` **[${c.nugetPackage.replace("Lumeo.", "")}]**`;
    const subs = Object.keys(c.subComponents);
    const subStr = subs.length ? ` _(sub-components: ${subs.join(", ")})_` : "";
    out += `- **${c.name}**${tag} — ${c.description}${subStr}\n`;
  }
}

out += `\n## Full-page patterns / blocks\n\nComposed examples built entirely from Lumeo components. Get the full Razor source with \`lumeo_get_pattern\`.\n\n`;
for (const p of a.patterns) out += `- **${p.title}** (\`${p.route}\`) — ${p.description}\n`;

out += `\n## Theme tokens\n\nThe ONLY legal colours. Use as Tailwind-style utilities: \`bg-{token}\`, \`text-{token}\`, \`border-{token}\`, \`ring-{token}\`, \`fill-{token}\`. Radius tokens → \`rounded-[var(--radius-…)]\`. Never raw hex/hsl; never \`dark:\` prefixes (dark mode swaps the variable values).\n\n`;
for (const t of a.themeTokens) out += `- \`${t.token}\` → \`${t.cssVar}\`\n`;

writeFileSync(resolve(here, "references/catalog.md"), out);
console.log(`wrote references/catalog.md (${out.length} bytes)`);
