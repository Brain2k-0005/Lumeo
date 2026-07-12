// Runs the pointer harness against all three engines sequentially (separate
// processes so one engine's crash can't take the others down) and prints a
// combined summary. Exit code is non-zero if any engine failed.
const { spawnSync } = require('child_process');
const path = require('path');

const ENGINES = ['chromium', 'firefox', 'webkit'];
const results = [];

for (const engine of ENGINES) {
  console.log(`\n########## ${engine} ##########`);
  const r = spawnSync(process.execPath, [path.join(__dirname, 'run.js'), engine], {
    stdio: 'inherit',
    cwd: __dirname,
  });
  results.push({ engine, code: r.status });
}

console.log('\n=== pointer-harness cross-engine summary ===');
let anyFailed = false;
for (const { engine, code } of results) {
  const ok = code === 0;
  if (!ok) anyFailed = true;
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${engine} (exit ${code})`);
}

process.exit(anyFailed ? 1 : 0);
