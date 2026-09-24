import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { PACKAGE_SETUP } from "../dist/installInfo.js";

// LU-16: lumeo_get_install told consumers to call `AddLumeoDataGrid()` / `AddLumeoCharts()`
// — service-collection extension methods that do not exist anywhere in the library (the
// ONLY one is `AddLumeo()` in src/Lumeo/Extensions/LumeoServiceExtensions.cs). Rather than
// re-hardcoding the "real" method names here (which would just move the drift one file
// over), this test derives the ground truth from the actual C# source the same way a
// consumer's compiler would see it, and fails if any `di` entry names a method that isn't
// a real `public static IServiceCollection Add*` extension somewhere under src/.

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "../../..");
const srcDir = join(repoRoot, "src");

/** Recursively collect every .cs file under `dir`. */
function collectCsFiles(dir) {
  const out = [];
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    const st = statSync(p);
    if (st.isDirectory()) {
      // obj/bin build output can contain generated copies — irrelevant and often absent
      // in a clean checkout, but skip them defensively so this test is build-state-agnostic.
      if (entry === "obj" || entry === "bin") continue;
      out.push(...collectCsFiles(p));
    } else if (entry.endsWith(".cs")) {
      out.push(p);
    }
  }
  return out;
}

function findRealAddMethods() {
  const found = new Set();
  const rx = /public\s+static\s+IServiceCollection\s+(Add[A-Za-z0-9_]*)\s*\(/g;
  for (const file of collectCsFiles(srcDir)) {
    const text = readFileSync(file, "utf8");
    let m;
    while ((m = rx.exec(text)) !== null) found.add(m[1]);
  }
  return found;
}

test("every AddXxx() named in an installInfo.ts `di` entry is a real extension method under src/", () => {
  const realMethods = findRealAddMethods();
  assert.ok(realMethods.has("AddLumeo"), "sanity check: AddLumeo() itself must be found in src/");

  const callRx = /\b(Add[A-Za-z0-9_]*)\s*\(/g;
  const missing = [];
  for (const [pkg, setup] of Object.entries(PACKAGE_SETUP)) {
    for (const line of setup.di) {
      let m;
      while ((m = callRx.exec(line)) !== null) {
        const method = m[1];
        if (!realMethods.has(method)) missing.push(`${pkg}: ${method}() (from "${line}")`);
      }
    }
  }
  assert.deepEqual(missing, [], `installInfo.ts names DI methods that don't exist:\n${missing.join("\n")}`);
});

test("regression: AddLumeoDataGrid / AddLumeoCharts / AddLumeoEditor / AddLumeoScheduler / " +
  "AddLumeoGantt / AddLumeoMotion are gone from every di[] entry", () => {
  const banned = [
    "AddLumeoDataGrid", "AddLumeoCharts", "AddLumeoEditor",
    "AddLumeoScheduler", "AddLumeoGantt", "AddLumeoMotion",
  ];
  for (const [pkg, setup] of Object.entries(PACKAGE_SETUP)) {
    const joined = setup.di.join(" ");
    for (const b of banned) {
      assert.ok(!joined.includes(`${b}(`), `${pkg} still references ${b}()`);
    }
  }
});
