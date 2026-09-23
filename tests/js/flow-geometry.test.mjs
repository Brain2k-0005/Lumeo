// flow.js <-> FlowGeometry.cs lockstep, JS half: the committed table
// (tests/Lumeo.Tests/Components/Flow/flow-geometry-table.json, which the bUnit suite checks the C#
// port against) must still be exactly what flow.js computes. If this fails, flow.js changed: make
// the same change in FlowGeometry.cs, then `node tests/js/flow-geometry-table.mjs --write`.
//
// Run with: node --test tests/js/flow-geometry.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildTable, tablePath } from './flow-geometry-table.mjs';

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
