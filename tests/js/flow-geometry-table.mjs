// Produces the lockstep table for FlowGeometry.cs <-> flow.js: every edge path generator for
// every handle-side combination on a fixed set of coordinates, plus fit-view, snapping and number
// formatting cases — all computed by flow.js's own functions (the __testing seam).
//
//   node tests/js/flow-geometry-table.mjs --write   regenerates the committed table
//   node --test tests/js/flow-geometry.test.mjs     asserts the committed table is still what
//                                                   flow.js produces
//
// The bUnit side (tests/Lumeo.Tests/Components/Flow/FlowGeometryLockstepTests.cs) asserts the C#
// port produces the same table. Change both sides together, then rerun with --write.

import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const flowJsPath = path.resolve(here, '../../src/Lumeo.Flow/wwwroot/js/flow.js');
export const tablePath = path.resolve(here, '../Lumeo.Tests/Components/Flow/flow-geometry-table.json');

const TYPES = ['bezier', 'smoothstep', 'step', 'straight'];
const SIDES = ['left', 'right', 'top', 'bottom'];
const COORDS = [
    [0, 0, 200, 100],
    [300, 50, 0, 0],
    [10.123, 20.456, 10.123, 220.789],
    [-50, -40, -50, -40],
    [0, 0, 0.0004, -0.0006],
    [123.4567, 89.1011, 456.789, -12.345],
    [100, 100, 60, 400],
];

export async function buildTable() {
    const { __testing: g } = await import(pathToFileURL(flowJsPath).href);
    const edges = [];
    for (const type of TYPES) {
        for (const sPos of SIDES) {
            for (const tPos of SIDES) {
                for (const [sx, sy, tx, ty] of COORDS) {
                    const p = g.edgePath(type, sx, sy, sPos, tx, ty, tPos);
                    edges.push({ type, sx, sy, sPos, tx, ty, tPos, d: p.d, labelX: p.labelX, labelY: p.labelY });
                }
            }
        }
    }

    const fits = [];
    const fitCases = [
        { rects: [{ x: 0, y: 0, width: 150, height: 40 }, { x: 400, y: 300, width: 150, height: 40 }], w: 800, h: 500, padding: 0.1, min: 0.25, max: 2 },
        { rects: [{ x: -200, y: -100, width: 100, height: 50 }], w: 800, h: 500, padding: 0.1, min: 0.25, max: 2 },
        { rects: [{ x: 0, y: 0, width: 5000, height: 3000 }], w: 800, h: 500, padding: 0.2, min: 0.25, max: 2 },
        { rects: [{ x: 10, y: 10, width: 20, height: 20 }], w: 1000, h: 600, padding: 0, min: 0.5, max: 4 },
        { rects: [{ x: 0, y: 0, width: 0, height: 0 }], w: 640, h: 480, padding: 0.1, min: 0.1, max: 10 },
    ];
    for (const c of fitCases) {
        const vp = g.fitView(c.rects, c.w, c.h, c.padding, c.min, c.max);
        fits.push({ ...c, x: vp.x, y: vp.y, zoom: vp.zoom });
    }

    const snaps = [];
    for (const [v, grid] of [[0, 16], [7.9, 16], [8, 16], [-8, 16], [-8.1, 16], [23.5, 15], [100, 0], [-0.4, 1], [12.5, 25]]) {
        snaps.push({ v, grid, r: g.snap(v, grid) });
    }

    const zooms = [];
    for (const [x, y, zoom, to, ax, ay] of [[0, 0, 1, 2, 400, 250], [-120.5, 33, 0.75, 0.5, 10, 490], [50, 50, 1.5, 1.5, 0, 0]]) {
        const vp = g.zoomAt({ x, y, zoom }, to, ax, ay);
        zooms.push({ x, y, zoom, to, ax, ay, rx: vp.x, ry: vp.y, rzoom: vp.zoom });
    }

    const formats = [];
    for (const v of [0, -0, 1, -1, 0.1, 0.0004, -0.0004, 0.0005, -0.0005, 1.0005, 123.4565, -123.4565, 1 / 3, 2 / 3, 1e6 + 0.1234, -99999.9995]) {
        formats.push({ v, s: g.fmt(v) });
    }

    return { edges, fits, snaps, zooms, formats };
}

if (process.argv.includes('--write')) {
    const table = await buildTable();
    writeFileSync(tablePath, JSON.stringify(table, null, 1) + '\n');
    console.log(`wrote ${tablePath}: ${table.edges.length} edge paths, ${table.fits.length} fits, ${table.snaps.length} snaps, ${table.zooms.length} zooms, ${table.formats.length} formats`);
}
