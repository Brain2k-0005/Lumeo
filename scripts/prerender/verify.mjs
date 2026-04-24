// scripts/prerender/verify.mjs
//
// Usage: node verify.mjs <base-url>
// Exit 0 = all assertions pass, exit 1 = at least one failed.
//
// Fetches a sample of live routes and asserts each response body contains
// expected content that only shows up in rendered HTML (not the stock shell).
// This catches regressions where prerender silently produces 3kB shells.

const baseUrl = (process.argv[2] || '').replace(/\/$/, '');
if (!baseUrl) {
    console.error('usage: node verify.mjs <base-url>');
    process.exit(1);
}

const checks = [
    { path: '/privacy',                 expect: 'Privacy policy' },
    { path: '/components/button',       expect: 'Button' },
    { path: '/components/consent-banner', expect: 'Consent Banner' },
    { path: '/components/input',        expect: 'Input' },
    { path: '/blocks/dashboard',        expect: 'Dashboard' },
];

let failed = 0;
for (const check of checks) {
    const res = await fetch(baseUrl + check.path);
    const body = await res.text();
    const ok = res.ok && body.includes(check.expect);
    console.log(`${ok ? '✓' : '✗'} ${check.path} — ${res.status}, ${(body.length / 1024).toFixed(1)}kB${ok ? '' : ` (missing "${check.expect}")`}`);
    if (!ok) failed++;
}

if (failed > 0) {
    console.error(`\n${failed} check(s) failed.`);
    process.exit(1);
}
console.log('\nAll checks passed.');
