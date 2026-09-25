// flow.js <-> FlowGeometry.cs lockstep, JS half: the committed table
// (tests/Lumeo.Tests/Components/Flow/flow-geometry-table.json, which the bUnit suite checks the C#
// port against) must still be exactly what flow.js computes. If this fails, flow.js changed: make
// the same change in FlowGeometry.cs, then `node tests/js/flow-geometry-table.mjs --write`.
//
// Run with: node --test tests/js/flow-geometry.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { buildTable, tablePath, flowJsPath } from './flow-geometry-table.mjs';

test('the committed lockstep table matches what flow.js computes today', async () => {
    const committed = JSON.parse(readFileSync(tablePath, 'utf8'));
    const fresh = JSON.parse(JSON.stringify(await buildTable()));
    assert.deepEqual(fresh, committed);
});

test('bezier: a target to the right of a right-facing source bends out by half the distance', async () => {
    const { edges } = JSON.parse(readFileSync(tablePath, 'utf8'));
    const e = edges.find(x => x.type === 'bezier' && x.sPos === 'right' && x.tPos === 'left' && x.sx === 0 && x.tx === 200);
    assert.equal(e.d, 'M0,0 C100,0 100,100 200,100');
});

// LU-19: anchorAlignedViewport (flow.js half of FlowGeometry.AnchorAlignedViewport — must stay in
// lockstep, same numbers as FlowCanvasSqlAnalystRound2FindingsTests.cs on the C# side).
test('LU-19: anchorAlignedViewport start/end/rtl placement', async () => {
    const { anchorAlignedViewport } = await import(pathToFileURL(flowJsPath).href).then(m => m.__testing);
    const anchor = { x: 0, y: 0, width: 100, height: 100 };

    const center = anchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, 'center');
    assert.equal(center.x, 800 / 2 - 50 * 0.92);
    assert.equal(center.y, 600 / 2 - 50 * 0.92);

    const start = anchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, 'start');
    assert.equal(start.x, 80);
    assert.equal(start.y, 60);

    const end = anchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, 'end');
    assert.equal(end.x, 800 - 80 - 100 * 0.92);
    assert.equal(end.y, 600 - 60 - 100 * 0.92);

    const startRtl = anchorAlignedViewport(anchor, 0.92, 800, 600, 0.1, 'start', true);
    assert.equal(startRtl.x, end.x); // RTL "start" mirrors LTR "end" horizontally ...
    assert.equal(startRtl.y, start.y); // ... but vertical placement is unaffected by rtl.
});
