// Regression test for field report #400 ("one bar reproducibly refuses to
// drag") — resolveHitModeFromGeometry is a pure function with no
// document/DOM dependency (unlike resolveHitMode, which wraps it with a real
// getBoundingClientRect() call), so it can be exercised directly here with
// plain {width, localX} numbers — same seam tests/js/echarts-interop-theme.
// test.mjs already uses for buildLumeoTheme.
//
// Run with: node --test tests/js/gantt-v3-hit-test.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const ganttV3JsPath = path.resolve(here, '../../src/Lumeo.Gantt/wwwroot/js/gantt-v3.js');

async function importGanttV3() {
    const url = pathToFileURL(ganttV3JsPath).href;
    return import(url);
}

test('ordinary-width bar (well above the 12px danger line): fixed 6px edges, move in the middle — unchanged from before this fix', async () => {
    const { __testing } = await importGanttV3();
    const width = 190; // the docs Gantt page's own "interviews"/"requirements" bar width at Day view
    assert.equal(__testing.resolveHitModeFromGeometry(width, 0), 'resize-start');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 6), 'resize-start'); // <= 6px from the left edge
    assert.equal(__testing.resolveHitModeFromGeometry(width, 7), 'move');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 95), 'move'); // dead center
    assert.equal(__testing.resolveHitModeFromGeometry(width, 183), 'move');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 184), 'resize-end'); // <= 6px from the right edge
    assert.equal(__testing.resolveHitModeFromGeometry(width, 190), 'resize-end');
});

test('bug (pre-fix behaviour, predicted WRONG value): a 10px-wide bar has NO move region under the old fixed-6px-both-edges math', () => {
    // Not calling into the module — this documents the OLD, buggy formula's
    // own prediction (localX <= 6 || width - localX <= 6) so the fix's own
    // assertions below have something concrete to contradict. At width=10,
    // every localX in [0,10] satisfies one of the two OLD conditions: [0,6]
    // via the first, [4,10] via the second — a full overlapping union with
    // nothing left over for 'move'.
    const oldResolve = (width, localX) => {
        if (localX <= 6) return 'resize-start';
        if (width - localX <= 6) return 'resize-end';
        return 'move';
    };
    for (let localX = 0; localX <= 10; localX++) {
        assert.notEqual(oldResolve(10, localX), 'move');
    }
});

test('fix: a 10px-wide bar (under the 12px danger line, still above the 8px BarGeometry floor) keeps a real move region', async () => {
    const { __testing } = await importGanttV3();
    const width = 10;
    // handle = width/3 = 3.333...px
    assert.equal(__testing.resolveHitModeFromGeometry(width, 0), 'resize-start');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 3), 'resize-start');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 5), 'move'); // dead center — the exact point the OLD math could never classify as 'move'
    assert.equal(__testing.resolveHitModeFromGeometry(width, 7), 'resize-end');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 10), 'resize-end');
});

test('fix: the guaranteed BarGeometry floor (8px, Math.max(8, x2-x1)) still keeps a non-empty, strictly positive move region', async () => {
    const { __testing } = await importGanttV3();
    const width = 8;
    // handle = 8/3 ≈ 2.667px — move region is (2.667, 5.333), width ≈ 2.667px, never zero/negative.
    assert.equal(__testing.resolveHitModeFromGeometry(width, 4), 'move'); // dead center
    assert.equal(__testing.resolveHitModeFromGeometry(width, 0), 'resize-start');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 8), 'resize-end');
});

test('fix: exactly at the 12px danger line (2*RESIZE_HANDLE_PX), handles shrink to width/3=4px rather than staying at the fixed 6px that would leave zero move region', async () => {
    const { __testing } = await importGanttV3();
    const width = 12;
    assert.equal(__testing.resolveHitModeFromGeometry(width, 4), 'resize-start'); // <= handle (4)
    assert.equal(__testing.resolveHitModeFromGeometry(width, 6), 'move'); // dead center — 4px move region (4,8)
    assert.equal(__testing.resolveHitModeFromGeometry(width, 8), 'resize-end'); // width-localX (4) <= handle (4)
});

test('fix: just above the danger line (13px) reverts to the ordinary fixed-6px classification, byte-identical to the pre-fix formula', async () => {
    const { __testing } = await importGanttV3();
    const width = 13;
    assert.equal(__testing.resolveHitModeFromGeometry(width, 6), 'resize-start');
    assert.equal(__testing.resolveHitModeFromGeometry(width, 6.5), 'move'); // the single-pixel move sliver this width leaves
    assert.equal(__testing.resolveHitModeFromGeometry(width, 7), 'resize-end');
});
