// Runs all four Phase 5 benchmark scripts in sequence and prints a compact
// summary of the headline numbers. Each script also writes its own JSON to
// scripts/perf/results/ (see README.md).
//
// CAUTION: wasm-boot.mjs needs a server started WITHOUT -p:LumeoPerfHeap=true
// to measure the boot cost real visitors get (see README.md "How to
// reproduce"). Running this file against a single perf-heap session is fine
// for re-checking the other three, but do not treat the wasm-boot number it
// produces as the published boot figure unless that session's heap is the
// default one.
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const scripts = ['datagrid-100k.mjs', 'datagrid-hotpaths.mjs', 'toast-burst.mjs', 'wasm-boot.mjs'];

for (const script of scripts) {
  console.log(`\n=== ${script} ===`);
  const result = spawnSync(process.execPath, [path.join(__dirname, script)], {
    stdio: 'inherit',
    env: process.env,
  });
  if (result.status !== 0) {
    console.error(`${script} failed with exit code ${result.status}`);
    process.exit(result.status ?? 1);
  }
}

console.log('\nAll Phase 5 benchmarks completed. See scripts/perf/results/*.json.');
