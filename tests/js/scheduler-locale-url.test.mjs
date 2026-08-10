// Codex review finding #3 (P2, PR #389, fix/scheduler-i18n) — standalone JS-level
// regression test for scheduler.js's locale-pack URL builder (_fcLocaleUrl).
//
// The repo has no JS unit-test harness for wwwroot interop files: every existing
// Scheduler test (tests/Lumeo.Tests/Components/Scheduler/*.cs) is a bUnit test that
// mocks the whole `./_content/Lumeo.Scheduler/js/scheduler.js` MODULE via
// SetupModule — the real scheduler.js file is never executed by those tests. This
// bug is pure JS logic (a URL string builder with no DOM/FullCalendar dependency),
// so it can only be exercised by actually running the file. Run with:
//   node --test tests/js/scheduler-locale-url.test.mjs
//
// Bug: _fcLocaleUrl only ever checked the `fullCalendarBase` convenience override
// and otherwise hard-coded the public esm.sh URL — it ignored `fullCalendarCore`,
// which is the ONE Scheduler CDN key actually registered in
// tools/Lumeo.RegistryGen/CdnDeps.cs. So a strict-CSP/offline deployment that
// self-hosted FullCalendar via that registered key still made a blocked public
// request for every locale pack, and silently fell back to English.
//
// Fix: locale URLs are now derived from FC_CORE itself (the already-resolved core
// URL, which honours both fullCalendarBase and fullCalendarCore), not re-derived
// from fullCalendarBase alone.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const schedulerJsPath = path.resolve(here, '../../src/Lumeo.Scheduler/wwwroot/js/scheduler.js');

// scheduler.js reads window.lumeoCdn at MODULE-EVALUATION time (top-level consts),
// mirroring how a real deployment's bootstrap script must set window.lumeoCdn
// before the module is imported. Each test needs a FRESH module evaluation with a
// different override, so a cache-busting query string forces Node to treat each
// import as a distinct module instance instead of reusing the ES module cache.
let cacheBust = 0;
async function importSchedulerWithCdn(lumeoCdn) {
    globalThis.window = { lumeoCdn };
    try {
        const url = pathToFileURL(schedulerJsPath).href + '?t=' + cacheBust++;
        return await import(url);
    } finally {
        delete globalThis.window;
    }
}

test('no override: locale URL resolves as a sub-path of the public esm.sh core package', async () => {
    const mod = await importSchedulerWithCdn({});
    const url = mod.__testing.fcLocaleUrl('de');
    assert.equal(url, 'https://esm.sh/@fullcalendar/core@6/locales/de.js');
});

test('fullCalendarCore override — the ONLY registered Scheduler key relevant here — is honoured by locale loading', async () => {
    const mod = await importSchedulerWithCdn({
        fullCalendarCore: 'https://internal.example.com/vendor/fullcalendar-core.js',
    });
    const url = mod.__testing.fcLocaleUrl('de');

    // Predicted WRONG value on the broken code: _fcLocaleUrl only checked
    // `fullCalendarBase` (unset here) and otherwise hard-coded the public esm.sh URL,
    // so this resolved to 'https://esm.sh/@fullcalendar/core@6/locales/de.js'
    // regardless of the fullCalendarCore override — a public request even though the
    // app self-hosted every advertised FullCalendar dependency via its registered key.
    assert.equal(url, 'https://internal.example.com/vendor/locales/de.js');
    assert.ok(
        !url.startsWith('https://esm.sh'),
        'must not fall back to the public CDN once fullCalendarCore is self-hosted',
    );
});

test('fullCalendarBase override still resolves locales relative to it (flat vendor convention)', async () => {
    const mod = await importSchedulerWithCdn({ fullCalendarBase: '/lib/fullcalendar/' });
    const url = mod.__testing.fcLocaleUrl('fr');
    assert.equal(url, '/lib/fullcalendar/locales/fr.js');
});

test('a bare esm-style fullCalendarCore override with a dotted version pin is not mistaken for a concrete file', async () => {
    // Regression guard for the fix's own file-vs-package-specifier heuristic: a
    // naive "does the last path segment contain a dot" check would misclassify a
    // semver version pin like "@6.1.10" as a filename and wrongly strip it.
    const mod = await importSchedulerWithCdn({
        fullCalendarCore: 'https://mycdn.example.com/proxy/@fullcalendar/core@6.1.10',
    });
    const url = mod.__testing.fcLocaleUrl('es');
    assert.equal(url, 'https://mycdn.example.com/proxy/@fullcalendar/core@6.1.10/locales/es.js');
});
