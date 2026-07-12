// Runs all four Phase 5 benchmark scripts in sequence and prints a compact
// summary of the headline numbers. Each script also writes its own JSON to
// scripts/perf/results/ (see README.md).
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
