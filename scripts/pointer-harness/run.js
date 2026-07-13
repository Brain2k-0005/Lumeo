const playwright = require('playwright-core');
const path = require('path');
const http = require('http');
const fs = require('fs');

// Engine is parameterized: `node run.js [chromium|firefox|webkit]`, falling
// back to the PW_ENGINE env var, defaulting to chromium. Every engine drives
// the SAME assertions below — this harness tests components.js's DOM/pointer
// contract, not anything Chromium-specific, so cross-engine parity is the
// point (Blazor Server ships to whatever browser the visitor brings).
const ENGINE = process.argv[2] || process.env.PW_ENGINE || 'chromium';
if (!['chromium', 'firefox', 'webkit'].includes(ENGINE)) {
  console.error(`Unknown engine "${ENGINE}" — expected chromium, firefox, or webkit.`);
  process.exit(1);
}

// Port varies per engine so `npm run test:all` can run engines in parallel
// without colliding on the same listener.
const PORT = 8935 + ['chromium', 'firefox', 'webkit'].indexOf(ENGINE);
const ROOT = __dirname;

// Always test the REPO file, never a stale snapshot — sync at runtime from
// src/, same as the Blazor-Server-latency and visual-regression legs.
fs.copyFileSync(
  path.join(ROOT, '..', '..', 'src', 'Lumeo', 'wwwroot', 'js', 'components.js'),
  path.join(ROOT, 'components.js'));

function serve() {
  const server = http.createServer((req, res) => {
    let p = req.url.split('?')[0];
    if (p === '/') p = '/harness.html';
    const fp = path.join(ROOT, p);
    fs.readFile(fp, (err, data) => {
      if (err) { res.writeHead(404); res.end('not found: ' + p); return; }
      const ct = fp.endsWith('.js') ? 'text/javascript' : fp.endsWith('.html') ? 'text/html' : 'text/plain';
      res.writeHead(200, { 'Content-Type': ct });
      res.end(data);
    });
  });
  return new Promise((resolve) => server.listen(PORT, () => resolve(server)));
}

let passCount = 0;
function assert(cond, msg) {
  if (!cond) throw new Error('ASSERT FAILED: ' + msg);
  passCount++;
  console.log('PASS: ' + msg);
}

(async () => {
  console.log(`=== pointer-harness — engine: ${ENGINE} (port ${PORT}) ===`);
  const server = await serve();
  const browser = await playwright[ENGINE].launch();
  const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
  page.on('pageerror', (e) => console.error('PAGEERROR', e));
  page.on('console', (m) => { if (m.type() === 'error') console.error('CONSOLE ERROR', m.text()); });
  await page.goto(`http://localhost:${PORT}/harness.html`);
  await page.waitForFunction(() => !!window.__C);

  // ---------------------------------------------------------------
  // TEST 1 — Resize unregister mid-drag cancels the drag (finding 1)
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <table id="tbl1">
        <thead><tr>
          <th id="th1" style="width:100px;min-width:100px" data-col-id="c1">
            Col1<span id="handle1" data-slot="datagrid-resize-handle"></span>
          </th>
        </tr></thead>
        <tbody><tr><td>cell</td></tr></tbody>
      </table>`;
  });
  const t1 = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnResize('handle1', window.__fakeDotNet, 50, 500);
    const handle = document.getElementById('handle1');
    const th = document.getElementById('th1');
    const rect = th.getBoundingClientRect();
    // pointerdown near the handle to start a drag
    handle.dispatchEvent(new PointerEvent('pointerdown', {
      pointerId: 1, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0,
    }));
    // drag the edge 80px to the right — width should grow live
    handle.dispatchEvent(new PointerEvent('pointermove', {
      pointerId: 1, clientX: rect.right + 75, clientY: rect.top + 5, bubbles: true, cancelable: true,
    }));
    return { widthDuringDrag: null }; // flushed via rAF, checked after a frame below
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const midDrag = await page.evaluate(() => {
    const th = document.getElementById('th1');
    return {
      widthDuringDrag: th.style.width,
      resizing: document.getElementById('handle1').dataset.resizing,
      bodyCursor: document.body.style.cursor,
      guidelineVisible: !!document.querySelector('[data-slot="datagrid-resize-guideline"]') &&
        document.querySelector('[data-slot="datagrid-resize-guideline"]').style.display !== 'none',
    };
  });
  assert(midDrag.widthDuringDrag === '180px', `resize live width applied mid-drag (got ${midDrag.widthDuringDrag})`);
  assert(midDrag.resizing === 'true', 'data-resizing set while dragging');
  assert(midDrag.bodyCursor === 'col-resize', 'body cursor set to col-resize while dragging');
  assert(midDrag.guidelineVisible === true, 'resize guideline visible while dragging');

  // Unmount mid-drag — no pointerup ever fires.
  const afterUnregister = await page.evaluate(() => {
    window.__C.unregisterColumnResize('handle1');
    const th = document.getElementById('th1');
    return {
      width: th.style.width,
      resizing: document.getElementById('handle1').dataset.resizing,
      bodyCursor: document.body.style.cursor,
      bodyUserSelect: document.body.style.userSelect,
      guidelineHidden: (() => {
        const g = document.querySelector('[data-slot="datagrid-resize-guideline"]');
        return !g || g.style.display === 'none';
      })(),
      dotnetCalls: window.__dotnetCalls.length,
    };
  });
  assert(afterUnregister.width === '100px', `unregister mid-drag restores pre-drag width (got ${afterUnregister.width})`);
  assert(afterUnregister.resizing === undefined, 'data-resizing cleared after unregister mid-drag');
  assert(afterUnregister.bodyCursor === '', 'body cursor cleared after unregister mid-drag');
  assert(afterUnregister.bodyUserSelect === '', 'body userSelect cleared after unregister mid-drag');
  assert(afterUnregister.guidelineHidden === true, 'resize guideline hidden after unregister mid-drag');
  assert(afterUnregister.dotnetCalls === 0, 'no OnColumnResizeCommit fired for an unregister-mid-drag abort');

  // ---------------------------------------------------------------
  // TEST 2 — Locked column is not displaceable / not targetable (finding 2)
  // Columns: A(100px, reorderable) | B(120px, LOCKED) | C(140px, reorderable)
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g2">
        <table id="tbl2" style="width:360px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="false" style="width:120px">B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:140px">
              <span data-reorder-grip>::</span>C</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td><td>c</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g2', window.__fakeDotNet);
  });
  const grip = await page.$('div[data-grid-id="g2"] th[data-col-id="A"] [data-reorder-grip]');
  const gripBox = await grip.boundingBox();
  const thC = await page.$('div[data-grid-id="g2"] th[data-col-id="C"]');
  const cBox = await thC.boundingBox();

  // Drag A's grip across locked B into C.
  await page.mouse.move(gripBox.x + gripBox.width / 2, gripBox.y + gripBox.height / 2);
  await page.mouse.down();
  // small move to arm (grip arms immediately, but move anyway)
  await page.mouse.move(gripBox.x + 20, gripBox.y + gripBox.height / 2, { steps: 3 });
  const lockedTxDuringPass = await page.evaluate(() => {
    const b = document.querySelector('div[data-grid-id="g2"] th[data-col-id="B"]');
    return getComputedStyle(b).transform;
  });
  // Move pointer to be over C.
  await page.mouse.move(cBox.x + cBox.width / 2, cBox.y + cBox.height / 2, { steps: 5 });
  const lockedTxOverC = await page.evaluate(() => {
    const b = document.querySelector('div[data-grid-id="g2"] th[data-col-id="B"]');
    return { bTransform: getComputedStyle(b).transform };
  });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const allCalls = await page.evaluate(() => window.__dotnetCalls);
  console.log('DEBUG dotnetCalls:', JSON.stringify(allCalls));
  const commit = allCalls.find((c) => c.method === 'OnColumnReorderCommit');

  assert(lockedTxDuringPass === 'none' || lockedTxDuringPass === '', `locked column B never displaced while dragging past it (got '${lockedTxDuringPass}')`);
  assert(lockedTxOverC.bTransform === 'none' || lockedTxOverC.bTransform === '', `locked column B stays put with pointer directly over it (got '${lockedTxOverC.bTransform}')`);
  assert(!!commit, 'reorder commit fired');
  assert(commit.args[2] === 'C', `commit targets reorderable C, never locked B (got target='${commit.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 3 — Rightward settle position with variable widths (finding 3, columns)
  // A(100px) dragged onto WIDER C(300px) via B(120px) — A should settle at
  // C.right - A.width, not C.left.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g3">
        <table id="tbl3" style="width:520px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px">
              <span data-reorder-grip>::</span>B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:300px">
              <span data-reorder-grip>::</span>C</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td><td>c</td></tr></tbody>
        </table>
      </div>`;
  });
  const rects3 = await page.evaluate(() => {
    const g = (id) => document.querySelector(`div[data-grid-id="g3"] th[data-col-id="${id}"]`).getBoundingClientRect();
    const a = g('A'), c = g('C');
    return { aLeft: a.left, aWidth: a.width, cLeft: c.left, cRight: c.right };
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g3', window.__fakeDotNet));
  const gripA3 = await page.$('div[data-grid-id="g3"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA3Box = await gripA3.boundingBox();
  const thC3 = await page.$('div[data-grid-id="g3"] th[data-col-id="C"]');
  const c3Box = await thC3.boundingBox();

  await page.mouse.move(gripA3Box.x + gripA3Box.width / 2, gripA3Box.y + gripA3Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(c3Box.x + c3Box.width / 2, c3Box.y + c3Box.height / 2, { steps: 8 });
  await page.mouse.up();
  // Let the settle transition (180ms) finish and the cleanup timeout (200ms) run.
  await page.waitForTimeout(300);
  const finalA = await page.evaluate(() => {
    const a = document.querySelector('div[data-grid-id="g3"] th[data-col-id="A"]');
    return a.getBoundingClientRect().left;
  });
  const expectedLeft = rects3.cRight - rects3.aWidth;
  const wrongOldLeft = rects3.cLeft;
  assert(Math.abs(finalA - expectedLeft) < 1,
    `A settles at target.right - A.width = ${expectedLeft} (got ${finalA}); old buggy formula would give ${wrongOldLeft}`);
  assert(Math.abs(finalA - wrongOldLeft) > 5, 'settle position is measurably different from the old (buggy) target.left formula');

  // ---------------------------------------------------------------
  // TEST 4 — Downward settle position with variable row heights (finding 3, rows)
  // Row0 (30px) dragged past Row1 (30px) onto Row2 (80px, "expanded") —
  // Row0 should settle at Row2.bottom - Row0.height, not Row2.top.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g4">
        <table id="tbl4">
          <tbody>
            <tr data-row-index="0" style="height:30px"><td><span data-row-reorder-grip>::</span>R0</td></tr>
            <tr data-row-index="1" style="height:30px"><td><span data-row-reorder-grip>::</span>R1</td></tr>
            <tr data-row-index="2" style="height:80px"><td><span data-row-reorder-grip>::</span>R2 (expanded)</td></tr>
          </tbody>
        </table>
      </div>`;
  });
  const rects4 = await page.evaluate(() => {
    const g = (i) => document.querySelector(`div[data-grid-id="g4"] tr[data-row-index="${i}"]`).getBoundingClientRect();
    const r0 = g(0), r2 = g(2);
    return { r0Height: r0.height, r2Top: r2.top, r2Bottom: r2.bottom };
  });
  await page.evaluate(() => window.__C.registerRowReorder('g4', window.__fakeDotNet));
  const grip0 = await page.$('div[data-grid-id="g4"] tr[data-row-index="0"] [data-row-reorder-grip]');
  const grip0Box = await grip0.boundingBox();
  const tr2 = await page.$('div[data-grid-id="g4"] tr[data-row-index="2"]');
  const tr2Box = await tr2.boundingBox();

  await page.mouse.move(grip0Box.x + grip0Box.width / 2, grip0Box.y + grip0Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(tr2Box.x + tr2Box.width / 2, tr2Box.y + tr2Box.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const finalR0Top = await page.evaluate(() => {
    const r0 = document.querySelector('div[data-grid-id="g4"] tr[data-row-index="0"]');
    return r0.getBoundingClientRect().top;
  });
  const expectedTop = rects4.r2Bottom - rects4.r0Height;
  const wrongOldTop = rects4.r2Top;
  assert(Math.abs(finalR0Top - expectedTop) < 1,
    `R0 settles at target.bottom - R0.height = ${expectedTop} (got ${finalR0Top}); old buggy formula would give ${wrongOldTop}`);
  assert(Math.abs(finalR0Top - wrongOldTop) > 5, 'row settle position is measurably different from the old (buggy) target.top formula');

  // ---------------------------------------------------------------
  // TEST 5 — Detail-row gap maps to its PARENT row, not the last row
  // (Codex round-3 #2). Row0/Row1/Row2 each 30px; Row1 has an expanded
  // detail <tr> (no data-row-index) of 60px sitting right after it.
  // Dropping mid-gap must target Row1 (its parent), not Row2 (the last row).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g5">
        <table id="tbl5">
          <tbody>
            <tr data-row-index="0" data-row-key="k0" style="height:30px"><td><span data-row-reorder-grip>::</span>R0</td></tr>
            <tr data-row-index="1" data-row-key="k1" style="height:30px"><td><span data-row-reorder-grip>::</span>R1</td></tr>
            <tr style="height:60px"><td>detail for R1</td></tr>
            <tr data-row-index="2" data-row-key="k2" style="height:30px"><td><span data-row-reorder-grip>::</span>R2</td></tr>
          </tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g5', window.__fakeDotNet);
  });
  const detailBox = await (await page.$('div[data-grid-id="g5"] tbody tr:nth-child(3)')).boundingBox();
  const grip0g5 = await page.$('div[data-grid-id="g5"] tr[data-row-index="0"] [data-row-reorder-grip]');
  const grip0g5Box = await grip0g5.boundingBox();
  await page.mouse.move(grip0g5Box.x + grip0g5Box.width / 2, grip0g5Box.y + grip0g5Box.height / 2);
  await page.mouse.down();
  // Land the pointer in the MIDDLE of the detail panel's own <tr> — no data-row-index
  // row covers this Y at all.
  await page.mouse.move(detailBox.x + detailBox.width / 2, detailBox.y + detailBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const commit5 = (await page.evaluate(() => window.__dotnetCalls)).find((c) => c.method === 'OnRowReorderCommit');
  assert(!!commit5, 'row reorder commit fired for a drop inside a detail-row gap');
  // Commits are keyed by stable row identity (data-row-key) now, not the plain
  // DOM index (Codex round-5 #6) — R0 dragged DOWN onto R1's detail gap.
  assert(commit5.args[2] === 'k1', `drop inside R1's detail gap targets R1 (key 'k1'), not the last row (got target='${commit5.args[2]}')`);

  // Detail <tr> must translate WITH its parent during the live shift, not stay
  // frozen in place while the parent row slides.
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g6">
        <table id="tbl6">
          <tbody>
            <tr data-row-index="0" data-row-key="k0" style="height:30px"><td><span data-row-reorder-grip>::</span>R0</td></tr>
            <tr data-row-index="1" data-row-key="k1" style="height:30px"><td><span data-row-reorder-grip>::</span>R1</td></tr>
            <tr style="height:60px"><td>detail for R1</td></tr>
            <tr data-row-index="2" data-row-key="k2" style="height:30px"><td><span data-row-reorder-grip>::</span>R2</td></tr>
          </tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerRowReorder('g6', window.__fakeDotNet));
  const grip0g6 = await page.$('div[data-grid-id="g6"] tr[data-row-index="0"] [data-row-reorder-grip]');
  const grip0g6Box = await grip0g6.boundingBox();
  const r2g6 = await page.$('div[data-grid-id="g6"] tr[data-row-index="2"]');
  const r2g6Box = await r2g6.boundingBox();
  await page.mouse.move(grip0g6Box.x + grip0g6Box.width / 2, grip0g6Box.y + grip0g6Box.height / 2);
  await page.mouse.down();
  // Drag R0 down past R1 (and its detail) onto R2 — R1 must live-shift up, and
  // its detail panel must shift up WITH it (not stay behind).
  await page.mouse.move(r2g6Box.x + r2g6Box.width / 2, r2g6Box.y + r2g6Box.height / 2, { steps: 8 });
  const detailTxDuringShift = await page.evaluate(() => {
    const detail = document.querySelector('div[data-grid-id="g6"] tbody tr:nth-child(3)');
    return getComputedStyle(detail).transform;
  });
  await page.mouse.up();
  await page.waitForTimeout(300);
  assert(detailTxDuringShift !== 'none' && detailTxDuringShift !== '',
    `R1's detail panel translates along with R1 during the live shift (got '${detailTxDuringShift}')`);

  // ---------------------------------------------------------------
  // TEST 6 — FLIP cleanup preserves consumer RowStyle inline styles and
  // never leaves willChange behind (Codex round-3 #3).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g7">
        <table id="tbl7">
          <tbody>
            <tr data-row-index="0" data-row-key="k0" style="height:30px; opacity: 0.42; position: relative; pointer-events: none;"><td>R0</td></tr>
            <tr data-row-index="1" data-row-key="k1" style="height:30px"><td>R1</td></tr>
          </tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__C.captureRowRects('g7');
    // Move R0 down 30px so animateRowReorder has a non-zero delta to animate.
    const r0 = document.querySelector('div[data-grid-id="g7"] tr[data-row-index="0"]');
    const r1 = document.querySelector('div[data-grid-id="g7"] tr[data-row-index="1"]');
    const tmp = r0.outerHTML; r0.outerHTML = r1.outerHTML; r1.outerHTML = tmp;
  });
  await page.evaluate(() => window.__C.animateRowReorder('g7', 50));
  await page.waitForTimeout(150);
  const afterFlip = await page.evaluate(() => {
    // The row carrying k0's data now sits at data-row-index=1 after the swap above.
    const r0 = document.querySelector('div[data-grid-id="g7"] tr[data-row-key="k0"]');
    return {
      opacity: r0.style.opacity, position: r0.style.position,
      pointerEvents: r0.style.pointerEvents, willChange: r0.style.willChange,
      transform: r0.style.transform,
    };
  });
  assert(afterFlip.opacity === '0.42', `consumer RowStyle opacity survives FLIP cleanup (got '${afterFlip.opacity}')`);
  assert(afterFlip.position === 'relative', `consumer RowStyle position survives FLIP cleanup (got '${afterFlip.position}')`);
  assert(afterFlip.pointerEvents === 'none', `consumer RowStyle pointer-events survives FLIP cleanup (got '${afterFlip.pointerEvents}')`);
  assert(afterFlip.willChange === '', `willChange is cleared after the FLIP animation settles (got '${afterFlip.willChange}')`);
  assert(afterFlip.transform === '', `FLIP transform is cleared after settling (got '${afterFlip.transform}')`);

  // ---------------------------------------------------------------
  // TEST 7 — Header-wide mouse init drops a stale pending drag once the
  // button is no longer held, instead of arming later with no button
  // down (Codex round-3 #4).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g8">
        <table id="tbl8" style="width:240px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g8', window.__fakeDotNet));
  const thB8 = await page.$('div[data-grid-id="g8"] th[data-col-id="B"]');
  const thB8Box = await thB8.boundingBox();
  // Press on header B (header-wide mouse init, NOT the grip) — this stays
  // UNARMED (no pointer capture yet). Move a few px within threshold, then
  // move OUTSIDE the grid entirely and release there — outside grid's own
  // pointerup listener (bound only to the grid element) never fires, so the
  // pending drag descriptor would otherwise linger.
  await page.mouse.move(thB8Box.x + thB8Box.width / 2, thB8Box.y + thB8Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(thB8Box.x + thB8Box.width / 2 + 2, thB8Box.y + thB8Box.height / 2, { steps: 2 });
  await page.mouse.move(thB8Box.x + thB8Box.width / 2, thB8Box.y + 400, { steps: 8 }); // well below the grid
  await page.mouse.up();
  // Now move the mouse back over the grid with NO button held — mouse
  // pointerIds are reused, so this used to be able to cross the stale
  // threshold (measured from the original press point) and arm a phantom
  // drag with no button down.
  await page.mouse.move(thB8Box.x + thB8Box.width / 2 + 40, thB8Box.y + thB8Box.height / 2, { steps: 8 });
  await page.waitForTimeout(50);
  const phantom = await page.evaluate(() => {
    const b = document.querySelector('div[data-grid-id="g8"] th[data-col-id="B"]');
    return { cursor: document.body.style.cursor, bOpacity: getComputedStyle(b).opacity };
  });
  assert(phantom.cursor === '', `no phantom drag arms with the button released (body cursor got '${phantom.cursor}')`);
  assert(phantom.bOpacity === '1', `header B isn't dimmed by a phantom armed drag (got opacity '${phantom.bOpacity}')`);

  // ---------------------------------------------------------------
  // TEST 8 — A nested grid's row grip must not arm the OUTER grid's drag
  // (Codex round-3 #5). Only OUTER's engine is registered here — the inner
  // grid has no pointerdown listener of its own to stopPropagation() the
  // gesture away (e.g. the window between Blazor rendering the inner grid's
  // grip markup and its own OnAfterRenderAsync calling registerRowReorder,
  // or an inner grid that renders row-reorder grip markup but isn't itself
  // wired up). That's exactly when the OLD, unscoped `grid.contains(grip)`
  // guard let the outer engine's delegated listener answer for a grip that
  // isn't its own — with both engines registered, the inner listener (being
  // the nearer DOM ancestor) always fires first and stops propagation,
  // masking the bug.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="outer">
        <table id="tblOuter">
          <tbody>
            <tr data-row-index="0" data-row-key="o0"><td><span data-row-reorder-grip>::</span>Outer R0
              <div data-grid-id="inner">
                <table id="tblInner">
                  <tbody>
                    <tr data-row-index="0" data-row-key="i0"><td><span data-row-reorder-grip>::</span>Inner R0</td></tr>
                    <tr data-row-index="1" data-row-key="i1"><td><span data-row-reorder-grip>::</span>Inner R1</td></tr>
                  </tbody>
                </table>
              </div>
            </td></tr>
            <tr data-row-index="1" data-row-key="o1"><td>Outer R1</td></tr>
          </tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('outer', window.__fakeDotNet); // inner NOT registered
  });
  const innerGrip0 = await page.$('div[data-grid-id="inner"] tr[data-row-index="0"] [data-row-reorder-grip]');
  const innerGrip0Box = await innerGrip0.boundingBox();
  const innerR1 = await page.$('div[data-grid-id="inner"] tr[data-row-index="1"]');
  const innerR1Box = await innerR1.boundingBox();
  await page.mouse.move(innerGrip0Box.x + innerGrip0Box.width / 2, innerGrip0Box.y + innerGrip0Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(innerR1Box.x + innerR1Box.width / 2, innerR1Box.y + innerR1Box.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const nestedCalls = await page.evaluate(() => window.__dotnetCalls);
  const outerCommit = nestedCalls.find((c) => c.method === 'OnRowReorderCommit' && c.args[0] === 'outer');
  assert(!outerCommit, `dragging the unregistered INNER grid's grip never commits an OUTER grid reorder (calls: ${JSON.stringify(nestedCalls)})`);

  // ---------------------------------------------------------------
  // TEST 9 — A nested grid's COLUMN grip must not arm the OUTER grid's
  // column drag (Codex round-4 #1 — mirrors TEST 8, for columns).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="outerCol">
        <table>
          <thead><tr><th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip>::</span>A</th></tr></thead>
          <tbody><tr><td>
            <div data-grid-id="innerCol">
              <table>
                <thead><tr>
                  <th data-col-id="X" data-col-pin="None" data-reorderable="true" style="width:100px">
                    <span data-reorder-grip>::</span>X</th>
                  <th data-col-id="Y" data-col-pin="None" data-reorderable="true" style="width:100px">Y</th>
                </tr></thead>
                <tbody><tr><td>x</td><td>y</td></tr></tbody>
              </table>
            </div>
          </td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('outerCol', window.__fakeDotNet); // inner NOT registered
  });
  const innerGripX = await page.$('div[data-grid-id="innerCol"] th[data-col-id="X"] [data-reorder-grip]');
  const innerGripXBox = await innerGripX.boundingBox();
  const innerThY = await page.$('div[data-grid-id="innerCol"] th[data-col-id="Y"]');
  const innerThYBox = await innerThY.boundingBox();
  await page.mouse.move(innerGripXBox.x + innerGripXBox.width / 2, innerGripXBox.y + innerGripXBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(innerThYBox.x + innerThYBox.width / 2, innerThYBox.y + innerThYBox.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const outerColCommit = (await page.evaluate(() => window.__dotnetCalls))
    .find((c) => c.method === 'OnColumnReorderCommit' && c.args[0] === 'outerCol');
  assert(!outerColCommit, `dragging the unregistered INNER grid's column grip never commits an OUTER grid reorder`);

  // ---------------------------------------------------------------
  // TEST 10 — Row candidates are filtered to THIS grid even for a
  // legitimately-owned grip: a nested row-reorderable grid living inside
  // one of THIS grid's own rows must not pollute the outer grid's own
  // row candidate list (Codex round-4 #2).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="outer2">
        <table><tbody>
          <tr data-row-index="0" data-row-key="o0"><td><span data-row-reorder-grip>::</span>Outer R0</td></tr>
          <tr data-row-index="1" data-row-key="o1"><td><span data-row-reorder-grip>::</span>Outer R1
            <div data-grid-id="inner2">
              <table><tbody>
                <tr data-row-index="0" data-row-key="i0"><td>Inner R0</td></tr>
                <tr data-row-index="1" data-row-key="i1"><td>Inner R1</td></tr>
              </tbody></table>
            </div>
          </td></tr>
          <tr data-row-index="2" data-row-key="o2"><td><span data-row-reorder-grip>::</span>Outer R2</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('outer2', window.__fakeDotNet);
  });
  const o0Grip = await page.$('div[data-grid-id="outer2"] tr[data-row-index="0"] [data-row-reorder-grip]');
  const o0GripBox = await o0Grip.boundingBox();
  const o2Row = await page.$('div[data-grid-id="outer2"] tr[data-row-key="o2"]');
  const o2RowBox = await o2Row.boundingBox();
  await page.mouse.move(o0GripBox.x + o0GripBox.width / 2, o0GripBox.y + o0GripBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(o2RowBox.x + o2RowBox.width / 2, o2RowBox.y + o2RowBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const outer2Commit = (await page.evaluate(() => window.__dotnetCalls))
    .find((c) => c.method === 'OnRowReorderCommit' && c.args[0] === 'outer2');
  assert(!!outer2Commit, 'outer2 row reorder commit fired');
  // Commits are keyed by stable row identity (data-row-key) now, not the plain
  // DOM index (Codex round-5 #6).
  assert(outer2Commit.args[1] === 'o0', `source key is outer2's own R0 ('o0') — got '${outer2Commit.args[1]}'`);
  assert(outer2Commit.args[2] === 'o2', `target key is outer2's own R2 ('o2'), not inflated by the inner grid's 2 rows (got '${outer2Commit.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 11 — Row settle accounts for the FULL detail-row band on both
  // the dragged side and the target side (Codex round-4 #3).
  // ---------------------------------------------------------------
  // 11a: dragged row has its own expanded detail (40px) — the gap it
  // vacates is 30+40=70px, not just its own 30px.
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g11a">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0" style="height:30px"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr style="height:40px"><td>detail for R0</td></tr>
          <tr data-row-index="1" data-row-key="k1" style="height:30px"><td>R1</td></tr>
          <tr data-row-index="2" data-row-key="k2" style="height:30px"><td>R2</td></tr>
        </tbody></table>
      </div>`;
  });
  const rects11a = await page.evaluate(() => {
    const g = (key) => document.querySelector(`div[data-grid-id="g11a"] tr[data-row-key="${key}"]`).getBoundingClientRect();
    const r0 = g('k0'), r2 = g('k2');
    return { r0Height: r0.height, r2Bottom: r2.bottom };
  });
  await page.evaluate(() => window.__C.registerRowReorder('g11a', window.__fakeDotNet));
  const grip0_11a = await page.$('div[data-grid-id="g11a"] tr[data-row-key="k0"] [data-row-reorder-grip]');
  const grip0_11aBox = await grip0_11a.boundingBox();
  const r2_11a = await page.$('div[data-grid-id="g11a"] tr[data-row-key="k2"]');
  const r2_11aBox = await r2_11a.boundingBox();
  await page.mouse.move(grip0_11aBox.x + grip0_11aBox.width / 2, grip0_11aBox.y + grip0_11aBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(r2_11aBox.x + r2_11aBox.width / 2, r2_11aBox.y + r2_11aBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const finalR0Top_11a = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g11a"] tr[data-row-key="k0"]').getBoundingClientRect().top);
  const expectedTop_11a = rects11a.r2Bottom - (rects11a.r0Height + 40); // full source band (row + detail)
  const wrongTop_11a = rects11a.r2Bottom - rects11a.r0Height; // old formula (parent-row-only)
  assert(Math.abs(finalR0Top_11a - expectedTop_11a) < 1,
    `R0 (with its own detail) settles using the full source band height (expected ${expectedTop_11a}, got ${finalR0Top_11a})`);
  assert(Math.abs(finalR0Top_11a - wrongTop_11a) > 5, 'settle position is measurably different from the old parent-row-only formula');

  // 11b: target row has its own expanded detail (50px) — the dragged row
  // must settle past the FULL target band, not just the target row itself.
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g11b">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0" style="height:30px"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="k1" style="height:30px"><td>R1</td></tr>
          <tr data-row-index="2" data-row-key="k2" style="height:30px"><td>R2</td></tr>
          <tr style="height:50px"><td>detail for R2</td></tr>
        </tbody></table>
      </div>`;
  });
  const rects11b = await page.evaluate(() => {
    const g = (key) => document.querySelector(`div[data-grid-id="g11b"] tr[data-row-key="${key}"]`).getBoundingClientRect();
    const r0 = g('k0'), r2 = g('k2');
    return { r0Height: r0.height, r2Bottom: r2.bottom };
  });
  await page.evaluate(() => window.__C.registerRowReorder('g11b', window.__fakeDotNet));
  const grip0_11b = await page.$('div[data-grid-id="g11b"] tr[data-row-key="k0"] [data-row-reorder-grip]');
  const grip0_11bBox = await grip0_11b.boundingBox();
  const r2_11b = await page.$('div[data-grid-id="g11b"] tr[data-row-key="k2"]');
  const r2_11bBox = await r2_11b.boundingBox();
  await page.mouse.move(grip0_11bBox.x + grip0_11bBox.width / 2, grip0_11bBox.y + grip0_11bBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(r2_11bBox.x + r2_11bBox.width / 2, r2_11bBox.y + r2_11bBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const finalR0Top_11b = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g11b"] tr[data-row-key="k0"]').getBoundingClientRect().top);
  const expectedTop_11b = (rects11b.r2Bottom + 50) - rects11b.r0Height; // full target band (row + its detail)
  const wrongTop_11b = rects11b.r2Bottom - rects11b.r0Height; // old formula (parent-row-only)
  assert(Math.abs(finalR0Top_11b - expectedTop_11b) < 1,
    `R0 settles past R2's FULL band including R2's own detail (expected ${expectedTop_11b}, got ${finalR0Top_11b})`);
  assert(Math.abs(finalR0Top_11b - wrongTop_11b) > 5, 'settle position is measurably different from the old parent-row-only formula');

  // ---------------------------------------------------------------
  // TEST 12 — Drag cleanup RESTORES consumer inline styles (opacity/
  // position/zIndex/pointerEvents) instead of blanking them, for both
  // the row and column engines (Codex round-4 #4).
  // ---------------------------------------------------------------
  // 12a: row engine.
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g12a">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0" style="height:30px; opacity:0.42; position:relative; z-index:5; pointer-events:none;">
            <td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="k1" style="height:30px"><td>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerRowReorder('g12a', window.__fakeDotNet));
  const grip0_12a = await page.$('div[data-grid-id="g12a"] tr[data-row-key="k0"] [data-row-reorder-grip]');
  const grip0_12aBox = await grip0_12a.boundingBox();
  const r1_12a = await page.$('div[data-grid-id="g12a"] tr[data-row-key="k1"]');
  const r1_12aBox = await r1_12a.boundingBox();
  await page.mouse.move(grip0_12aBox.x + grip0_12aBox.width / 2, grip0_12aBox.y + grip0_12aBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1_12aBox.x + r1_12aBox.width / 2, r1_12aBox.y + r1_12aBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const rowStyleAfter = await page.evaluate(() => {
    const r0 = document.querySelector('div[data-grid-id="g12a"] tr[data-row-key="k0"]');
    return { opacity: r0.style.opacity, position: r0.style.position, zIndex: r0.style.zIndex, pointerEvents: r0.style.pointerEvents };
  });
  assert(rowStyleAfter.opacity === '0.42', `consumer RowStyle opacity survives drag cleanup (got '${rowStyleAfter.opacity}')`);
  assert(rowStyleAfter.position === 'relative', `consumer RowStyle position survives drag cleanup (got '${rowStyleAfter.position}')`);
  assert(rowStyleAfter.zIndex === '5', `consumer RowStyle zIndex survives drag cleanup (got '${rowStyleAfter.zIndex}')`);
  assert(rowStyleAfter.pointerEvents === 'none', `consumer RowStyle pointer-events survives drag cleanup (got '${rowStyleAfter.pointerEvents}')`);

  // 12b: column engine (header cell + its body cell both carry ColumnStyle).
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g12b">
        <table style="width:240px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px; opacity:0.42; position:relative; z-index:5; pointer-events:none;">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">B</th>
          </tr></thead>
          <tbody><tr>
            <td style="opacity:0.42; position:relative; z-index:5; pointer-events:none;">a</td>
            <td>b</td>
          </tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g12b', window.__fakeDotNet));
  const gripA_12b = await page.$('div[data-grid-id="g12b"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA_12bBox = await gripA_12b.boundingBox();
  const thB_12b = await page.$('div[data-grid-id="g12b"] th[data-col-id="B"]');
  const thB_12bBox = await thB_12b.boundingBox();
  await page.mouse.move(gripA_12bBox.x + gripA_12bBox.width / 2, gripA_12bBox.y + gripA_12bBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(thB_12bBox.x + thB_12bBox.width / 2, thB_12bBox.y + thB_12bBox.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const colStyleAfter = await page.evaluate(() => {
    const th = document.querySelector('div[data-grid-id="g12b"] th[data-col-id="A"]');
    const td = document.querySelector('div[data-grid-id="g12b"] tbody td');
    return {
      thOpacity: th.style.opacity, thPosition: th.style.position, thZIndex: th.style.zIndex, thPointerEvents: th.style.pointerEvents,
      tdOpacity: td.style.opacity, tdPosition: td.style.position, tdZIndex: td.style.zIndex, tdPointerEvents: td.style.pointerEvents,
    };
  });
  assert(colStyleAfter.thOpacity === '0.42', `consumer ColumnStyle opacity survives drag cleanup on the header (got '${colStyleAfter.thOpacity}')`);
  assert(colStyleAfter.thPosition === 'relative', `consumer ColumnStyle position survives drag cleanup on the header (got '${colStyleAfter.thPosition}')`);
  assert(colStyleAfter.thZIndex === '5', `consumer ColumnStyle zIndex survives drag cleanup on the header (got '${colStyleAfter.thZIndex}')`);
  assert(colStyleAfter.thPointerEvents === 'none', `consumer ColumnStyle pointer-events survives drag cleanup on the header (got '${colStyleAfter.thPointerEvents}')`);
  assert(colStyleAfter.tdOpacity === '0.42', `consumer ColumnStyle opacity survives drag cleanup on the body cell (got '${colStyleAfter.tdOpacity}')`);
  assert(colStyleAfter.tdPointerEvents === 'none', `consumer ColumnStyle pointer-events survives drag cleanup on the body cell (got '${colStyleAfter.tdPointerEvents}')`);

  // ---------------------------------------------------------------
  // TEST 13 — A pending (unarmed) header-wide drag is cleared by a
  // window-level pointerup even when no move ever crosses this grid
  // again — closing the mouse-pointerId-reuse window a later UNRELATED
  // press-and-drag could otherwise resurrect (Codex round-4 #5).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g13">
        <table id="tbl13" style="width:240px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>
      <div id="elsewhere" style="position:fixed; top:500px; left:10px; width:50px; height:50px;"></div>`;
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g13', window.__fakeDotNet));
  const thB13 = await page.$('div[data-grid-id="g13"] th[data-col-id="B"]');
  const thB13Box = await thB13.boundingBox();
  // Press header B (header-wide init, stays unarmed), tiny move, release
  // OUTSIDE the grid — the window pointerup should clear the descriptor
  // immediately (not just on a later move with no button held).
  await page.mouse.move(thB13Box.x + thB13Box.width / 2, thB13Box.y + thB13Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(thB13Box.x + thB13Box.width / 2 + 2, thB13Box.y + thB13Box.height / 2, { steps: 2 });
  await page.mouse.move(35, 520, { steps: 8 });
  await page.mouse.up();
  // A completely UNRELATED press-and-hold elsewhere, then drag back across
  // the grid far enough to cross the ORIGINAL press's movement threshold —
  // reusing the same synthetic mouse pointerId. Without the window
  // pointerup fix, the stale unarmed descriptor could still be sitting
  // there and re-arm against header B's ORIGINAL press point.
  await page.mouse.move(35, 520);
  await page.mouse.down();
  await page.mouse.move(thB13Box.x + thB13Box.width / 2 + 40, thB13Box.y + thB13Box.height / 2, { steps: 8 });
  await page.waitForTimeout(50);
  const staleState = await page.evaluate(() => {
    const b = document.querySelector('div[data-grid-id="g13"] th[data-col-id="B"]');
    return { cursor: document.body.style.cursor, bOpacity: getComputedStyle(b).opacity };
  });
  await page.mouse.up();
  assert(staleState.cursor === '', `an unrelated later press-drag doesn't re-arm the stale pending descriptor (body cursor got '${staleState.cursor}')`);
  assert(staleState.bOpacity === '1', `header B isn't dimmed by a resurrected stale drag (got opacity '${staleState.bOpacity}')`);

  // WEBKIT-ONLY TEST ISOLATION: TEST13 exercises the mouse-pointerId-reuse
  // edge case with an "unrelated" press-and-drag gesture over header B while
  // no drag is armed. Bisection proved this specific sequence — and only
  // this one, of the 13 tests before it — corrupts TEST14's result under
  // WebKit ONLY (Chromium/Firefox unaffected): re-running the suite with
  // TEST13's body skipped makes TEST14 pass; explicitly unregistering every
  // grid registered so far (the obvious "leaked listener" theory) does NOT
  // fix it, so the corruption isn't a leftover per-grid `drag` descriptor —
  // something in WebKit's own event/gesture-recognizer state survives past
  // TEST13's own (passing) assertions. Reloading the page resets that
  // engine-internal state cleanly without touching product code or loosening
  // any assertion; every earlier test rebuilds its fixture from a wiped
  // #host anyway, so a fresh navigation here is a legitimate test-isolation
  // boundary, not a workaround for the assertion itself.
  await page.reload();
  await page.waitForFunction(() => !!window.__C);

  // ---------------------------------------------------------------
  // TEST 14 — Sibling shift for a column skipping a LOCKED column is
  // computed from the full locked-preserving final projection, not a
  // uniform ±sourceWidth shift (Codex round-4 #6). Equal 100px columns
  // A / locked B / C: dragging A onto C must land C exactly where A
  // started (zero overlap with B), not on top of B (the old formula's
  // -sourceWidth-only shift lands C exactly on B when widths are equal).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g14">
        <table id="tbl14" style="width:300px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="false" style="width:100px">B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>C</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td><td>c</td></tr></tbody>
        </table>
      </div>`;
  });
  const rects14 = await page.evaluate(() => {
    const g = (id) => document.querySelector(`div[data-grid-id="g14"] th[data-col-id="${id}"]`).getBoundingClientRect();
    const a = g('A'), b = g('B');
    return { aLeft: a.left, bLeft: b.left, bRight: b.right };
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g14', window.__fakeDotNet));
  const gripA14 = await page.$('div[data-grid-id="g14"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA14Box = await gripA14.boundingBox();
  const thC14 = await page.$('div[data-grid-id="g14"] th[data-col-id="C"]');
  const thC14Box = await thC14.boundingBox();
  await page.mouse.move(gripA14Box.x + gripA14Box.width / 2, gripA14Box.y + gripA14Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(thC14Box.x + thC14Box.width / 2, thC14Box.y + thC14Box.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const final14 = await page.evaluate(() => {
    const a = document.querySelector('div[data-grid-id="g14"] th[data-col-id="A"]').getBoundingClientRect();
    const b = document.querySelector('div[data-grid-id="g14"] th[data-col-id="B"]').getBoundingClientRect();
    const c = document.querySelector('div[data-grid-id="g14"] th[data-col-id="C"]').getBoundingClientRect();
    return { aLeft: a.left, bLeft: b.left, cLeft: c.left, cRight: c.right };
  });
  assert(Math.abs(final14.bLeft - rects14.bLeft) < 1, `locked column B never moves (got left ${final14.bLeft}, was ${rects14.bLeft})`);
  assert(Math.abs(final14.cLeft - rects14.aLeft) < 1,
    `C lands exactly at A's original slot (expected ${rects14.aLeft}, got ${final14.cLeft}) — old formula would land it on B (${rects14.bLeft})`);
  assert(final14.cRight <= rects14.bLeft + 1, `C does not overlap locked B (C right=${final14.cRight}, B left=${rects14.bLeft})`);
  assert(Math.abs(final14.aLeft - rects14.bRight) < 1, `A settles immediately after B's unmoved slot (expected ${rects14.bRight}, got ${final14.aLeft})`);

  // ---------------------------------------------------------------
  // TEST 15 — captureRowRects/animateRowReorder clear and animate a
  // detail <tr>'s leftover transform, not just its parent's — a
  // committed drag intentionally leaves a translateY on the dragged
  // row's (and shifted siblings') detail panel through the handoff
  // (Codex round-4 #7).
  //
  // R1's leftover transform is deliberately -24px, NOT -30px (R0's own
  // height, i.e. exactly the shift produced by moving R1 from index 1 to
  // index 0 below). -30px would make oldTop (measured WITH the leftover
  // transform applied, pre-reorder) numerically equal to newTop (measured
  // AFTER the DOM move, transform cleared) — a coherent drag/commit handoff
  // where the live-preview position already matched the final DOM slot, so
  // delta is genuinely 0 and animateRowReorder correctly skips animating.
  // -24px leaves a real 6px residual so 15b actually exercises the
  // mid-flight interpolation path instead of the (also-correct) delta<1
  // bail-out path.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g15">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0" style="height:30px"><td>R0</td></tr>
          <tr data-row-index="1" data-row-key="k1" style="height:30px; transform:translateY(-24px)"><td>R1</td></tr>
          <tr style="height:40px; transform:translateY(-24px)"><td>detail for R1</td></tr>
        </tbody></table>
      </div>`;
  });
  // 15a: capture must clear the leftover transform on BOTH the row and its detail.
  await page.evaluate(() => window.__C.captureRowRects('g15'));
  const afterCapture15 = await page.evaluate(() => {
    const r1 = document.querySelector('div[data-grid-id="g15"] tr[data-row-key="k1"]');
    const detail = r1.nextElementSibling;
    return { r1Transform: r1.style.transform, detailTransform: detail.style.transform };
  });
  assert(afterCapture15.r1Transform === '', `captureRowRects clears R1's leftover transform (got '${afterCapture15.r1Transform}')`);
  assert(afterCapture15.detailTransform === '', `captureRowRects clears R1's detail leftover transform too (got '${afterCapture15.detailTransform}')`);

  // 15b: animate — move R1 (+ its detail) up in the DOM so its top changes,
  // then confirm the detail rides the SAME inverse-then-release animation.
  await page.evaluate(() => {
    const tbody = document.querySelector('div[data-grid-id="g15"] table tbody');
    const r0 = document.querySelector('div[data-grid-id="g15"] tr[data-row-key="k0"]');
    const r1 = document.querySelector('div[data-grid-id="g15"] tr[data-row-key="k1"]');
    const detail = r1.nextElementSibling;
    tbody.insertBefore(r1, r0);
    tbody.insertBefore(detail, r0);
  });
  await page.evaluate(() => window.__C.animateRowReorder('g15', 60));
  // animateRowReorder sets the inline transform to '' in the SAME
  // synchronous call that starts the CSS transition (the forced reflow in
  // between is what makes the browser actually animate from the pre-reflow
  // value) — the INLINE style is '' immediately, but the RENDERED/computed
  // transform is still mid-interpolation for the 60ms duration, so check
  // getComputedStyle shortly after instead of the inline attribute.
  await page.waitForTimeout(20);
  const midAnimate15 = await page.evaluate(() => {
    const r1 = document.querySelector('div[data-grid-id="g15"] tr[data-row-key="k1"]');
    const detail = r1.nextElementSibling;
    return { r1Transform: getComputedStyle(r1).transform, detailTransform: getComputedStyle(detail).transform };
  });
  assert(midAnimate15.r1Transform !== 'none', 'R1 is mid-FLIP-animation with a non-identity computed transform');
  assert(midAnimate15.detailTransform !== 'none',
    `R1's detail rides the same FLIP animation as R1 (got computed transform '${midAnimate15.detailTransform}')`);
  await page.waitForTimeout(150);
  const afterAnimate15 = await page.evaluate(() => {
    const r1 = document.querySelector('div[data-grid-id="g15"] tr[data-row-key="k1"]');
    const detail = r1.nextElementSibling;
    return { r1Transform: r1.style.transform, detailTransform: detail.style.transform };
  });
  assert(afterAnimate15.r1Transform === '', `R1's transform is cleared once the FLIP animation settles (got '${afterAnimate15.r1Transform}')`);
  assert(afterAnimate15.detailTransform === '',
    `R1's detail transform is ALSO cleared once the animation settles, not left offset (got '${afterAnimate15.detailTransform}')`);

  // ---------------------------------------------------------------
  // TEST 16 — captureColumnRects preserves consumer ColumnStyle inline
  // styles on EVERY column it captures, not just the ones a drag actively
  // touched. This FLIP-capture pass runs on every reorder commit over ALL
  // header+body cells (ownedByGrid sweep), so a hand-rolled clear of
  // opacity/position/zIndex/pointerEvents here — instead of delegating to
  // clearFlipStyles (transform/transition/willChange only) — would
  // permanently erase a consumer's own ColumnStyle on any UNTOUCHED column
  // the moment ANY column in the grid is first reordered (Blazor skips
  // rewriting an unchanged style attribute on the next render). Symmetric
  // fix to Codex round-4 #4, found via the round-4 symmetry sweep: that
  // finding only patched armDrag/finishDrag, missing this second instance
  // of the exact same anti-pattern living in the FLIP-capture path (rows'
  // captureRowRects/clearRowFlipStyles never had it).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g16">
        <table style="width:200px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" style="width:100px; opacity:0.42; position:relative; z-index:5; pointer-events:none;">A</th>
            <th data-col-id="B" data-col-pin="None" style="width:100px">B</th>
          </tr></thead>
          <tbody><tr>
            <td style="opacity:0.42; position:relative; z-index:5; pointer-events:none;">a</td>
            <td>b</td>
          </tr></tbody>
        </table>
      </div>`;
  });
  // No drag ever touches column A here — captureColumnRects is called
  // directly, exactly as DataGrid's own commit handler would after ANY
  // OTHER column in this grid reorders.
  await page.evaluate(() => window.__C.captureColumnRects('g16'));
  const afterCapture16 = await page.evaluate(() => {
    const th = document.querySelector('div[data-grid-id="g16"] th[data-col-id="A"]');
    const td = document.querySelector('div[data-grid-id="g16"] tbody td');
    return {
      thOpacity: th.style.opacity, thPosition: th.style.position, thZIndex: th.style.zIndex, thPointerEvents: th.style.pointerEvents,
      tdOpacity: td.style.opacity, tdPosition: td.style.position, tdZIndex: td.style.zIndex, tdPointerEvents: td.style.pointerEvents,
    };
  });
  assert(afterCapture16.thOpacity === '0.42', `consumer ColumnStyle opacity on an UNTOUCHED column survives captureColumnRects (got '${afterCapture16.thOpacity}')`);
  assert(afterCapture16.thPosition === 'relative', `consumer ColumnStyle position on an UNTOUCHED column survives captureColumnRects (got '${afterCapture16.thPosition}')`);
  assert(afterCapture16.thZIndex === '5', `consumer ColumnStyle zIndex on an UNTOUCHED column survives captureColumnRects (got '${afterCapture16.thZIndex}')`);
  assert(afterCapture16.thPointerEvents === 'none', `consumer ColumnStyle pointer-events on an UNTOUCHED column survives captureColumnRects (got '${afterCapture16.thPointerEvents}')`);
  assert(afterCapture16.tdOpacity === '0.42', `consumer ColumnStyle opacity on an UNTOUCHED column's body cell survives captureColumnRects (got '${afterCapture16.tdOpacity}')`);
  assert(afterCapture16.tdPointerEvents === 'none', `consumer ColumnStyle pointer-events on an UNTOUCHED column's body cell survives captureColumnRects (got '${afterCapture16.tdPointerEvents}')`);

  // ---------------------------------------------------------------
  // TEST 17 — Upward detail-band slot: dragging a row UP onto the gap right
  // after row i's detail must target the AFTER-i slot, not the BEFORE-i slot
  // (Codex round-5 #1). 4 rows: R0, R1(with a 40px detail), R2, R3(grip).
  // Drag R3 up into the R1-detail gap — must land between R1 and R2, i.e.
  // commit target = R2's key ('k2'), not R1's key ('k1').
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g17">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0" style="height:30px"><td>R0</td></tr>
          <tr data-row-index="1" data-row-key="k1" style="height:30px"><td>R1</td></tr>
          <tr style="height:40px"><td>detail for R1</td></tr>
          <tr data-row-index="2" data-row-key="k2" style="height:30px"><td>R2</td></tr>
          <tr data-row-index="3" data-row-key="k3" style="height:30px"><td><span data-row-reorder-grip>::</span>R3</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g17', window.__fakeDotNet);
  });
  const detailBox17 = await (await page.$('div[data-grid-id="g17"] tbody tr:nth-child(3)')).boundingBox();
  const grip3_17 = await page.$('div[data-grid-id="g17"] tr[data-row-key="k3"] [data-row-reorder-grip]');
  const grip3_17Box = await grip3_17.boundingBox();
  await page.mouse.move(grip3_17Box.x + grip3_17Box.width / 2, grip3_17Box.y + grip3_17Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(detailBox17.x + detailBox17.width / 2, detailBox17.y + detailBox17.height / 2, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const commit17 = (await page.evaluate(() => window.__dotnetCalls)).find((c) => c.method === 'OnRowReorderCommit');
  assert(!!commit17, 'row reorder commit fired for an upward drag into a detail-row gap');
  assert(commit17.args[1] === 'k3', `commit source is R3 (got '${commit17.args[1]}')`);
  assert(commit17.args[2] === 'k2', `upward drag into R1's detail gap lands AFTER R1 — R2's slot (key 'k2'), not BEFORE R1 (got target='${commit17.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 18 — A second pointerdown while a COLUMN drag is already live must
  // be ignored, not overwrite the single `drag` descriptor (Codex round-5 #3).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g18">
        <table style="width:200px">
          <thead><tr>
            <th id="g18A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th id="g18B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g18', window.__fakeDotNet));
  const result18 = await page.evaluate(() => {
    const gripA = document.querySelector('#g18A [data-reorder-grip]');
    const gripB = document.querySelector('#g18B [data-reorder-grip]');
    const rectA = gripA.getBoundingClientRect();
    const rectB = gripB.getBoundingClientRect();
    // First pointer arms via the grip immediately.
    gripA.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, clientX: rectA.x, clientY: rectA.y, bubbles: true, cancelable: true, button: 0 }));
    const aOpacityAfterFirstDown = document.getElementById('g18A').style.opacity;
    // A second pointer (different pointerId — a second touch point) presses
    // B's grip WHILE the first drag is still live.
    gripB.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: rectB.x, clientY: rectB.y, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' }));
    const bOpacityAfterSecondDown = document.getElementById('g18B').style.opacity;
    return { aOpacityAfterFirstDown, bOpacityAfterSecondDown };
  });
  assert(result18.aOpacityAfterFirstDown === '0.8', 'first pointerdown arms its own column drag (A dimmed)');
  assert(result18.bOpacityAfterSecondDown !== '0.8', `a second pointerdown while a column drag is live is ignored — B never arms (got opacity '${result18.bOpacityAfterSecondDown}')`);
  // Releasing the FIRST (un-clobbered) pointer must still finish/commit normally.
  await page.evaluate(() => {
    const gripA = document.querySelector('#g18A [data-reorder-grip]');
    const rectB = document.getElementById('g18B').getBoundingClientRect();
    gripA.dispatchEvent(new PointerEvent('pointermove', { pointerId: 1, clientX: rectB.x + rectB.width / 2, clientY: rectB.y + rectB.height / 2, bubbles: true, cancelable: true }));
    gripA.dispatchEvent(new PointerEvent('pointerup', { pointerId: 1, clientX: rectB.x + rectB.width / 2, clientY: rectB.y + rectB.height / 2, bubbles: true, cancelable: true }));
  });
  await page.waitForTimeout(300);
  const commit18 = (await page.evaluate(() => window.__dotnetCalls)).find((c) => c.method === 'OnColumnReorderCommit' && c.args[0] === 'g18');
  assert(!!commit18, 'the first (un-clobbered) column drag still commits normally after the ignored second pointerdown');
  assert(commit18.args[1] === 'A', `commit source is A, the originally-armed drag (got '${commit18.args[1]}')`);

  // ---------------------------------------------------------------
  // TEST 19 — Same guard, ROW engine mirror of TEST 18 (Codex round-5 #3).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g19">
        <table><tbody>
          <tr data-row-index="0" data-row-key="k0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="k1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g19', window.__fakeDotNet);
  });
  const grip0_19 = await page.$('div[data-grid-id="g19"] tr[data-row-key="k0"] [data-row-reorder-grip]');
  const grip0_19Box = await grip0_19.boundingBox();
  const grip1_19 = await page.$('div[data-grid-id="g19"] tr[data-row-key="k1"] [data-row-reorder-grip]');
  const grip1_19Box = await grip1_19.boundingBox();
  await page.mouse.move(grip0_19Box.x + grip0_19Box.width / 2, grip0_19Box.y + grip0_19Box.height / 2);
  await page.mouse.down();
  const armedR0_19 = await page.evaluate(() => document.querySelector('div[data-grid-id="g19"] tr[data-row-key="k0"]').style.opacity);
  await page.evaluate(({ x, y }) => {
    const grip1 = document.querySelector('div[data-grid-id="g19"] tr[data-row-key="k1"] [data-row-reorder-grip]');
    grip1.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 99, clientX: x, clientY: y, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' }));
  }, { x: grip1_19Box.x + grip1_19Box.width / 2, y: grip1_19Box.y + grip1_19Box.height / 2 });
  const armedR1_19 = await page.evaluate(() => document.querySelector('div[data-grid-id="g19"] tr[data-row-key="k1"]').style.opacity);
  await page.mouse.up();
  await page.waitForTimeout(300);
  const rowCommits19 = (await page.evaluate(() => window.__dotnetCalls)).filter((c) => c.method === 'OnRowReorderCommit');
  assert(armedR0_19 === '0.8', 'R0 grip pointerdown arms its own row drag');
  assert(armedR1_19 !== '0.8', `a second pointerdown on R1's grip while R0's row drag is live is ignored — R1 never arms (got opacity '${armedR1_19}')`);
  assert(rowCommits19.length <= 1, `at most the original (un-clobbered) row drag can commit — no second drag was ever started (got ${rowCommits19.length} commits)`);

  // ---------------------------------------------------------------
  // TEST 20 — Same guard, RESIZE engine (Codex round-5 #3, mirrored per the
  // task's explicit "both engines + resize" scope). A second finger touching
  // the SAME handle mid-drag must not clobber activePointerId/startWidth —
  // the FIRST pointer's own pointerup must still be recognized and commit once.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <table><thead><tr>
        <th id="th20" style="width:100px;min-width:100px" data-col-id="c20">
          Col<span id="handle20" data-slot="datagrid-resize-handle"></span>
        </th>
      </tr></thead><tbody><tr><td>cell</td></tr></tbody></table>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnResize('handle20', window.__fakeDotNet, 50, 500);
    const handle = document.getElementById('handle20');
    const th = document.getElementById('th20');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, clientX: rect.right, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 1, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const midState20 = await page.evaluate(() => document.getElementById('th20').style.width);
  // A second finger touches the SAME handle while the first resize is live.
  await page.evaluate(() => {
    const handle = document.getElementById('handle20');
    const th = document.getElementById('th20');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' }));
  });
  await page.evaluate(() => {
    const handle = document.getElementById('handle20');
    const th = document.getElementById('th20');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 1, clientX: rect.right + 40 - 100, clientY: rect.top + 5, bubbles: true, cancelable: true }));
  });
  const calls20 = await page.evaluate(() => window.__dotnetCalls);
  const commits20 = calls20.filter((c) => c.method === 'OnColumnResizeCommit');
  assert(midState20 === '140px', `first pointer's resize applies live width before the second pointerdown arrives (got ${midState20})`);
  assert(commits20.length === 1, `exactly one resize commit fires — the ignored concurrent pointerdown didn't spawn a second drag (got ${commits20.length}: ${JSON.stringify(commits20)})`);
  assert(commits20[0].args[1] === 140, `commit reflects the FIRST pointer's drag width (140), undisturbed by the ignored second pointerdown (got ${commits20[0].args[1]})`);

  // ---------------------------------------------------------------
  // TEST 21 — A double-click's two no-movement pointerdown/pointerup pairs
  // must not fire a spurious resize commit; only the real dblclick auto-fit
  // commit fires (Codex round-5 #4).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <table><thead><tr>
        <th id="th21" style="width:100px;min-width:100px" data-col-id="c21">
          <span>ab</span><span id="handle21" data-slot="datagrid-resize-handle"></span>
        </th>
      </tr></thead><tbody><tr><td>cell</td></tr></tbody></table>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnResize('handle21', window.__fakeDotNet, 50, 500);
  });
  await page.evaluate(() => {
    const handle = document.getElementById('handle21');
    const th = document.getElementById('th21');
    const rect = th.getBoundingClientRect();
    const x = rect.right - 5, y = rect.top + 5;
    const fire = (type, pid) => handle.dispatchEvent(new PointerEvent(type, { pointerId: pid, clientX: x, clientY: y, bubbles: true, cancelable: true, button: 0 }));
    // Real browsers deliver TWO full pointerdown/pointerup pairs (no movement
    // between down and up) before dblclick fires.
    fire('pointerdown', 1); fire('pointerup', 1);
    fire('pointerdown', 1); fire('pointerup', 1);
    handle.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
  });
  await page.waitForTimeout(20);
  const commits21 = (await page.evaluate(() => window.__dotnetCalls)).filter((c) => c.method === 'OnColumnResizeCommit');
  assert(commits21.length === 1, `a double-click fires exactly ONE resize commit — the auto-fit one, not a spurious no-op commit from either pointerup pair (got ${commits21.length}: ${JSON.stringify(commits21)})`);
  assert(commits21[0].args[2] === true, `the single commit is the real auto-fit one (autoFit=true), not a no-op drag commit (got ${JSON.stringify(commits21[0].args)})`);

  // ---------------------------------------------------------------
  // TEST 22 — armDrag must not override a PINNED column's `position: sticky`
  // with `position: relative`; a non-pinned column's cells still get
  // `position: relative` for the drag z-index lift (Codex round-5 #5).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g22">
        <table style="width:400px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="Left" data-reorderable="true" style="width:100px; position:sticky; left:0px;">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="A2" data-col-pin="Left" data-reorderable="true" style="width:100px; position:sticky; left:100px;">A2</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:100px">C</th>
          </tr></thead>
          <tbody><tr>
            <td style="position:sticky; left:0px;">a</td>
            <td style="position:sticky; left:100px;">a2</td>
            <td>b</td>
            <td>c</td>
          </tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => window.__C.registerColumnReorder('g22', window.__fakeDotNet));

  const gripA22 = await page.$('div[data-grid-id="g22"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA22Box = await gripA22.boundingBox();
  await page.mouse.move(gripA22Box.x + gripA22Box.width / 2, gripA22Box.y + gripA22Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(gripA22Box.x + 10, gripA22Box.y + gripA22Box.height / 2, { steps: 3 });
  const pinnedDuringDrag = await page.evaluate(() => {
    const th = document.querySelector('div[data-grid-id="g22"] th[data-col-id="A"]');
    const td = document.querySelector('div[data-grid-id="g22"] tbody td');
    return { thPosition: th.style.position, tdPosition: td.style.position };
  });
  await page.mouse.up();
  await page.waitForTimeout(300);
  assert(pinnedDuringDrag.thPosition === 'sticky', `dragging a PINNED column keeps its header cell's position:sticky (got '${pinnedDuringDrag.thPosition}')`);
  assert(pinnedDuringDrag.tdPosition === 'sticky', `dragging a PINNED column keeps its body cell's position:sticky (got '${pinnedDuringDrag.tdPosition}')`);

  const gripB22 = await page.$('div[data-grid-id="g22"] th[data-col-id="B"] [data-reorder-grip]');
  const gripB22Box = await gripB22.boundingBox();
  await page.mouse.move(gripB22Box.x + gripB22Box.width / 2, gripB22Box.y + gripB22Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(gripB22Box.x + 10, gripB22Box.y + gripB22Box.height / 2, { steps: 3 });
  const nonPinnedDuringDrag = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g22"] th[data-col-id="B"]').style.position);
  await page.mouse.up();
  await page.waitForTimeout(300);
  assert(nonPinnedDuringDrag === 'relative', `dragging a NON-pinned column still gets position:relative for the drag z-index lift (got '${nonPinnedDuringDrag}')`);

  // ---------------------------------------------------------------
  // TEST 23 — Cross-engine drag arbiter (round-6 finding #1): a live
  // column-reorder drag must block a resize pointerdown on the SAME grid
  // (and vice versa) — not just each engine's own second-pointer self-guard
  // (TEST 18-20).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g23">
        <table style="width:300px">
          <thead><tr>
            <th id="th23a" data-col-id="A" data-reorderable="true" style="width:100px;min-width:100px">
              <span data-reorder-grip id="grip23a">::</span>A<span id="handle23a" data-slot="datagrid-resize-handle"></span>
            </th>
            <th id="th23b" data-col-id="B" data-reorderable="true" style="width:100px;min-width:100px">
              <span data-reorder-grip id="grip23b">::</span>B<span id="handle23b" data-slot="datagrid-resize-handle"></span>
            </th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__C.registerColumnResize('handle23a', window.__fakeDotNet, 50, 500);
    window.__C.registerColumnResize('handle23b', window.__fakeDotNet, 50, 500);
    window.__C.registerColumnReorder('g23', window.__fakeDotNet);
  });

  // 23a — REORDER already live blocks a RESIZE pointerdown on a different column.
  const r23a = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const gripA = document.getElementById('grip23a');
    gripA.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, clientX: 5, clientY: 5, bubbles: true, cancelable: true, button: 0 }));
    const thA = document.getElementById('th23a');
    const armedOpacity = thA.style.opacity; // grip-initiated arm is immediate — no movement threshold
    const handleB = document.getElementById('handle23b');
    const thB = document.getElementById('th23b');
    const rectB = thB.getBoundingClientRect();
    handleB.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: rectB.right - 5, clientY: rectB.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handleB.dispatchEvent(new PointerEvent('pointermove', { pointerId: 2, clientX: rectB.right + 60, clientY: rectB.top + 5, bubbles: true, cancelable: true }));
    const blockedResizing = handleB.dataset.resizing;
    const blockedWidth = thB.style.width;
    // Release the reorder drag (no projected move — same slot, no commit expected).
    gripA.dispatchEvent(new PointerEvent('pointerup', { pointerId: 1, clientX: 5, clientY: 5, bubbles: true, cancelable: true }));
    return { armedOpacity, blockedResizing, blockedWidth };
  });
  assert(r23a.armedOpacity === '0.8', `reorder drag armed on grip A (opacity got '${r23a.armedOpacity}')`);
  assert(r23a.blockedResizing === undefined, `resize on column B never starts while A's reorder drag is live (data-resizing got '${r23a.blockedResizing}')`);
  assert(r23a.blockedWidth === '100px', `column B's width is untouched at its original 100px — the blocked resize never applied a live width (got '${r23a.blockedWidth}')`);
  // 23a's grip release was armed (immediate-arm, grip-initiated) with no
  // projected move, so it takes the cancel branch — but cancel still runs
  // through the same post-release settle window as a commit (round-10 #3:
  // the arbiter is now held until that queued timeout actually fires, not
  // released synchronously at drop). Let it elapse so 23b starts from a
  // clean, unclaimed arbiter.
  await page.waitForTimeout(220);

  // 23b — RESIZE already live blocks a REORDER (grip) pointerdown on a different column.
  const r23b = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const handleA = document.getElementById('handle23a');
    const thA = document.getElementById('th23a');
    const rectA = thA.getBoundingClientRect();
    handleA.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, clientX: rectA.right - 5, clientY: rectA.top + 5, bubbles: true, cancelable: true, button: 0 }));
    const resizingA = handleA.dataset.resizing;
    const gripB = document.getElementById('grip23b');
    const thB = document.getElementById('th23b');
    gripB.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: 5, clientY: 5, bubbles: true, cancelable: true, button: 0 }));
    const blockedOpacity = thB.style.opacity;
    // Release the resize drag.
    handleA.dispatchEvent(new PointerEvent('pointerup', { pointerId: 1, clientX: rectA.right - 5, clientY: rectA.top + 5, bubbles: true, cancelable: true }));
    return { resizingA, blockedOpacity };
  });
  assert(r23b.resizingA === 'true', `resize drag started on column A (data-resizing got '${r23b.resizingA}')`);
  assert(r23b.blockedOpacity === '', `column B's reorder drag never arms while A's resize is live (opacity got '${r23b.blockedOpacity}')`);

  // ---------------------------------------------------------------
  // TEST 24 — Keyboard resize at a Min/MaxWidth clamp must not fire a
  // duplicate no-op commit (round-6 finding #4); a genuine nudge still
  // commits exactly once.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <table><thead><tr>
        <th id="th24" style="width:50px;min-width:50px" data-col-id="c24">
          Col<span id="handle24" data-slot="datagrid-resize-handle"></span>
        </th>
      </tr></thead><tbody><tr><td>cell</td></tr></tbody></table>`;
  });
  const r24 = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnResize('handle24', window.__fakeDotNet, 50, 500);
    // Already at MinWidth (50) — nudging further into the clamp must compute
    // the SAME width and skip the commit entirely.
    const clampedReturn = window.__C.nudgeColumnResize('handle24', -10);
    const clampedCalls = window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit').length;
    // A genuine nudge (away from the clamp) must still commit exactly once.
    const grownReturn = window.__C.nudgeColumnResize('handle24', 10);
    const grownCalls = window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit');
    return { clampedReturn, clampedCalls, grownReturn, grownCallsCount: grownCalls.length, grownArgs: grownCalls[0]?.args };
  });
  assert(r24.clampedReturn === 50, `nudge into the Min clamp returns the unchanged width (got ${r24.clampedReturn})`);
  assert(r24.clampedCalls === 0, `nudge into the Min clamp fires NO OnColumnResizeCommit (got ${r24.clampedCalls})`);
  assert(r24.grownReturn === 60, `a genuine nudge away from the clamp still computes the new width (got ${r24.grownReturn})`);
  assert(r24.grownCallsCount === 1, `a genuine nudge still commits exactly once (got ${r24.grownCallsCount})`);
  assert(r24.grownArgs && r24.grownArgs[1] === 60, `the genuine nudge's commit carries the new width 60 (got ${JSON.stringify(r24.grownArgs)})`);

  // ---------------------------------------------------------------
  // TEST 25 — SAME-engine concurrent drags (round-7 finding #1): two
  // DIFFERENT resize handles in the SAME grid each keep their own local
  // `isDragging`, so only the shared arbiter can see the second pointerdown
  // collides with a still-live drag of the SAME engine name.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g25">
        <table>
          <thead><tr>
            <th id="th25a" data-col-id="A" style="width:100px;min-width:100px">
              A<span id="handle25a" data-slot="datagrid-resize-handle"></span>
            </th>
            <th id="th25b" data-col-id="B" style="width:100px;min-width:100px">
              B<span id="handle25b" data-slot="datagrid-resize-handle"></span>
            </th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__C.registerColumnResize('handle25a', window.__fakeDotNet, 50, 500);
    window.__C.registerColumnResize('handle25b', window.__fakeDotNet, 50, 500);
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const handleA = document.getElementById('handle25a');
    const thA = document.getElementById('th25a');
    const rectA = thA.getBoundingClientRect();
    handleA.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 1, clientX: rectA.right, clientY: rectA.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handleA.dispatchEvent(new PointerEvent('pointermove', { pointerId: 1, clientX: rectA.right + 40, clientY: rectA.top + 5, bubbles: true, cancelable: true }));
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const midA25 = await page.evaluate(() => document.getElementById('th25a').style.width);
  // A SECOND, DIFFERENT resize handle in the SAME grid starts its own drag
  // while A's is still live.
  const attempt25b = await page.evaluate(() => {
    const handleB = document.getElementById('handle25b');
    const thB = document.getElementById('th25b');
    const rectB = thB.getBoundingClientRect();
    handleB.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: rectB.right, clientY: rectB.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handleB.dispatchEvent(new PointerEvent('pointermove', { pointerId: 2, clientX: rectB.right + 40, clientY: rectB.top + 5, bubbles: true, cancelable: true }));
    return { resizingB: handleB.dataset.resizing };
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const afterB25 = await page.evaluate(() => ({
    widthB: document.getElementById('th25b').style.width,
    widthA: document.getElementById('th25a').style.width, // A's own drag must be undisturbed
  }));
  assert(midA25 === '140px', `first handle's own resize applies live width before the second handle's pointerdown arrives (got ${midA25})`);
  assert(attempt25b.resizingB === undefined, `second (different) resize handle in the SAME grid never starts while A's resize is live (data-resizing got '${attempt25b.resizingB}')`);
  assert(afterB25.widthB === '100px', `blocked second handle's column stays at its original width (got ${afterB25.widthB})`);
  assert(afterB25.widthA === '140px', `first handle's own live drag is undisturbed by the blocked second attempt (got ${afterB25.widthA})`);
  // Release A's drag — only ONE commit fires (the blocked B attempt never
  // started a drag to commit in the first place).
  await page.evaluate(() => {
    const handleA = document.getElementById('handle25a');
    const rectA = document.getElementById('th25a').getBoundingClientRect();
    handleA.dispatchEvent(new PointerEvent('pointerup', { pointerId: 1, clientX: rectA.right, clientY: rectA.top + 5, bubbles: true, cancelable: true }));
  });
  const commits25 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(commits25.length === 1, `exactly one resize commit fires (A's) — the blocked B attempt never started, so it can't double-commit (got ${commits25.length})`);

  // ---------------------------------------------------------------
  // TEST 26 — Arbiter leak on stale drops (round-7 finding #2): a header-wide
  // mouse press that never armed, then a pointermove reaching the grid with
  // NO button held and NO pointerup ever dispatched anywhere in the document
  // (simulating a release the window pointerup listener itself never saw —
  // e.g. outside the browser entirely) must still release the arbiter claim
  // it took at pointerdown, or every later drag on this grid is rejected
  // forever.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g26">
        <table>
          <thead><tr>
            <th id="th26a" data-col-id="A" data-reorderable="true" style="width:100px;min-width:100px">A<span id="handle26a" data-slot="datagrid-resize-handle"></span></th>
            <th id="th26b" data-col-id="B" data-reorderable="true" style="width:100px;min-width:100px">B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__C.registerColumnReorder('g26', window.__fakeDotNet);
    window.__C.registerColumnResize('handle26a', window.__fakeDotNet, 50, 500);
  });
  await page.evaluate(() => {
    const thB = document.getElementById('th26b');
    const rectB = thB.getBoundingClientRect();
    // Header-wide mouse press on B (NOT the grip) claims the arbiter as
    // 'column-reorder' and stays UNARMED — the exact "pressed but never
    // armed" precondition the finding describes.
    thB.dispatchEvent(new PointerEvent('pointerdown', {
      pointerId: 1, pointerType: 'mouse', clientX: rectB.x + rectB.width / 2, clientY: rectB.y + rectB.height / 2,
      bubbles: true, cancelable: true, button: 0, buttons: 1,
    }));
    // buttons=0 on this pointermove — reaches ONLY the stale-move fallback's
    // release path (no real pointerup fired, so onWindowPointerUp never runs).
    thB.dispatchEvent(new PointerEvent('pointermove', {
      pointerId: 1, pointerType: 'mouse', clientX: rectB.x + rectB.width / 2 + 3, clientY: rectB.y + rectB.height / 2,
      buttons: 0, bubbles: true, cancelable: true,
    }));
  });
  // The arbiter claim must be free again — a resize on column A's handle
  // (same grid) must start normally.
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const handleA = document.getElementById('handle26a');
    const thA = document.getElementById('th26a');
    const rectA = thA.getBoundingClientRect();
    handleA.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: rectA.right, clientY: rectA.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handleA.dispatchEvent(new PointerEvent('pointermove', { pointerId: 2, clientX: rectA.right + 40, clientY: rectA.top + 5, bubbles: true, cancelable: true }));
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const after26 = await page.evaluate(() => ({
    resizing: document.getElementById('handle26a').dataset.resizing,
    width: document.getElementById('th26a').style.width,
  }));
  assert(after26.resizing === 'true', `resize starts normally after the stale drop released the arbiter (data-resizing got '${after26.resizing}')`);
  assert(after26.width === '140px', `resize applies its live width after the stale drop released the arbiter (got ${after26.width})`);
  // Release the still-live resize drag for a clean commit.
  await page.evaluate(() => {
    const handleA = document.getElementById('handle26a');
    const rectA = document.getElementById('th26a').getBoundingClientRect();
    handleA.dispatchEvent(new PointerEvent('pointerup', { pointerId: 2, clientX: rectA.right, clientY: rectA.top + 5, bubbles: true, cancelable: true }));
  });
  const commits26 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(commits26.length === 1, `the post-leak-fix resize still commits normally (got ${commits26.length})`);

  // ---------------------------------------------------------------
  // TEST 27 — Column reorder: unregister DURING the post-release settle
  // window clears the engine-owned transforms and cancels the delayed
  // commit (round-9 #1). finishDrag sets `drag = null` synchronously on
  // release and defers the actual commit to a ~200ms setTimeout; before the
  // fix, cancelActiveDrag was a no-op once `drag` was null, so unregister
  // racing that window left A/B's cells stuck translated with no
  // OnColumnReorderCommit ever firing to hand them to CaptureColumnRects.
  //
  // Extended (round-10 #3, disabled-mid-settle): a resize handle lives on B
  // too, so this also proves the cross-engine arbiter token is HELD through
  // the whole settle window (a competing resize on this same grid must be
  // blocked while pendingSettle is live — before the round-10 fix,
  // finishDrag released the token synchronously at drop time, so this
  // resize attempt would have wrongly succeeded), and that
  // unregisterColumnReorder's mid-settle teardown (the disabled/no-longer-
  // eligible path a Reorderable flip drives) releases that token
  // immediately rather than leaking it until some settle timeout that will
  // never fire.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g27">
        <table id="tbl27">
          <thead><tr>
            <th id="th27a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th id="th27b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:120px">
              <span data-reorder-grip>::</span>B<span id="handle27b" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g27', window.__fakeDotNet);
    window.__C.registerColumnResize('handle27b', window.__fakeDotNet, 50, 500);
  });
  const grip27 = await page.$('div[data-grid-id="g27"] th[data-col-id="A"] [data-reorder-grip]');
  const grip27Box = await grip27.boundingBox();
  const th27b = await page.$('#th27b');
  const th27bBox = await th27b.boundingBox();
  await page.mouse.move(grip27Box.x + grip27Box.width / 2, grip27Box.y + grip27Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th27bBox.x + th27bBox.width / 2, th27bBox.y + th27bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Still inside the ~200ms settle window (nothing has unregistered or timed
  // out yet) — a competing resize on the SAME grid must be blocked because
  // the arbiter token is still held for the pending commit (round-10 #3).
  const blockedDuringSettle27 = await page.evaluate(() => {
    const handle = document.getElementById('handle27b');
    const th = document.getElementById('th27b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 9, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 9, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { resizing: handle.dataset.resizing, width: th.style.width };
  });
  assert(blockedDuringSettle27.resizing === undefined, `resize on B is blocked while A's column-reorder commit is still settling (data-resizing got '${blockedDuringSettle27.resizing}')`);
  assert(blockedDuringSettle27.width === '120px', `B's width untouched at 120px — the blocked resize never applied a live width (got '${blockedDuringSettle27.width}')`);

  // The commit is deferred ~200ms past release — unregister immediately,
  // still inside that settle window, before it can fire.
  await page.evaluate(() => window.__C.unregisterColumnReorder('g27'));
  // The unregister-driven teardown must release the arbiter token right
  // away (cancelActiveDrag's pendingSettle branch) — a resize attempt
  // immediately after must now succeed, proving the token isn't leaked
  // until a settle timeout that was just canceled and will never fire.
  const resizing27 = await page.evaluate(() => {
    const handle = document.getElementById('handle27b');
    const th = document.getElementById('th27b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 10, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 10, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return handle.dataset.resizing;
  });
  // The width write is rAF-throttled (registerColumnResize coalesces
  // pointermoves into one DOM mutation per frame) — wait a frame before
  // reading it back, mirrors TEST 1's own rAF wait.
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const freedAfterUnregister27 = await page.evaluate(() => {
    const handle = document.getElementById('handle27b');
    const th = document.getElementById('th27b');
    const rect = th.getBoundingClientRect();
    const width = th.style.width;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 10, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { width };
  });
  assert(resizing27 === 'true', `resize on B succeeds right after unregister-during-settle frees the arbiter (data-resizing got '${resizing27}')`);
  assert(freedAfterUnregister27.width === '185px', `resize on B actually applied once the arbiter was free (got '${freedAfterUnregister27.width}')`);

  await page.waitForTimeout(300);
  const after27 = await page.evaluate(() => ({
    aTransform: document.getElementById('th27a').style.transform,
    bTransform: document.getElementById('th27b').style.transform,
    commits: window.__dotnetCalls.filter((c) => c.method === 'OnColumnReorderCommit' && c.args[0] === 'g27').length,
  }));
  assert(after27.commits === 0, `unregister during the settle window cancels the delayed column commit (got ${after27.commits} commits)`);
  assert(after27.aTransform === '', `dragged column A's transform is cleared by unregister-during-settle (got '${after27.aTransform}')`);
  assert(after27.bTransform === '', `shifted sibling B's transform is cleared by unregister-during-settle (got '${after27.bTransform}')`);

  // ---------------------------------------------------------------
  // TEST 28 — Row reorder: unregister DURING the post-release settle window
  // clears the engine-owned transforms and cancels the delayed commit
  // (round-9 #2, vertical mirror of TEST 27). Extended (round-10 #3,
  // disabled-mid-settle) the same way as TEST 27: a resize handle on the
  // SAME grid proves the arbiter token is held for the whole row-reorder
  // settle window and released immediately by unregisterRowReorder's
  // mid-settle teardown.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g28">
        <table>
          <thead><tr>
            <th id="th28" data-col-id="rc" style="width:100px;min-width:100px">
              <span id="handle28" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody>
          <tr data-row-index="0" data-row-key="r0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="r1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g28', window.__fakeDotNet);
    window.__C.registerColumnResize('handle28', window.__fakeDotNet, 50, 500);
  });
  const grip28 = await page.$('div[data-grid-id="g28"] tr[data-row-key="r0"] [data-row-reorder-grip]');
  const grip28Box = await grip28.boundingBox();
  const r1Row = await page.$('div[data-grid-id="g28"] tr[data-row-key="r1"]');
  const r1RowBox = await r1Row.boundingBox();
  await page.mouse.move(grip28Box.x + grip28Box.width / 2, grip28Box.y + grip28Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1RowBox.x + r1RowBox.width / 2, r1RowBox.y + r1RowBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Still inside the row-reorder settle window — a competing resize on the
  // same grid must be blocked (round-10 #3, mirrors TEST 27's column check).
  const blockedDuringSettle28 = await page.evaluate(() => {
    const handle = document.getElementById('handle28');
    const th = document.getElementById('th28');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 9, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 9, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { resizing: handle.dataset.resizing, width: th.style.width };
  });
  assert(blockedDuringSettle28.resizing === undefined, `resize is blocked while the row-reorder commit is still settling (data-resizing got '${blockedDuringSettle28.resizing}')`);
  assert(blockedDuringSettle28.width === '100px', `resize column's width untouched at 100px — the blocked resize never applied a live width (got '${blockedDuringSettle28.width}')`);

  await page.evaluate(() => window.__C.unregisterRowReorder('g28'));
  // Freed immediately by unregisterRowReorder's mid-settle teardown.
  const resizing28 = await page.evaluate(() => {
    const handle = document.getElementById('handle28');
    const th = document.getElementById('th28');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 10, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 10, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return handle.dataset.resizing;
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const freedAfterUnregister28 = await page.evaluate(() => {
    const handle = document.getElementById('handle28');
    const th = document.getElementById('th28');
    const rect = th.getBoundingClientRect();
    const width = th.style.width;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 10, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { width };
  });
  assert(resizing28 === 'true', `resize succeeds right after unregister-during-settle frees the arbiter (data-resizing got '${resizing28}')`);
  assert(freedAfterUnregister28.width === '165px', `resize actually applied once the arbiter was free (got '${freedAfterUnregister28.width}')`);

  await page.waitForTimeout(300);
  const after28 = await page.evaluate(() => ({
    r0Transform: document.querySelector('div[data-grid-id="g28"] tr[data-row-key="r0"]').style.transform,
    r1Transform: document.querySelector('div[data-grid-id="g28"] tr[data-row-key="r1"]').style.transform,
    commits: window.__dotnetCalls.filter((c) => c.method === 'OnRowReorderCommit' && c.args[0] === 'g28').length,
  }));
  assert(after28.commits === 0, `unregister during the settle window cancels the delayed row commit (got ${after28.commits} commits)`);
  assert(after28.r0Transform === '', `dragged row R0's transform is cleared by unregister-during-settle (got '${after28.r0Transform}')`);
  assert(after28.r1Transform === '', `shifted sibling R1's transform is cleared by unregister-during-settle (got '${after28.r1Transform}')`);

  // ---------------------------------------------------------------
  // TEST 29 — Column reorder, NATURAL settle completion (round-10 #3): with
  // no unregister racing it, the arbiter token stays claimed for the whole
  // ~200ms settle window and is only released once the queued
  // OnColumnReorderCommit has actually been dispatched — before this fix,
  // finishDrag released the token synchronously at drop time, so a
  // competing resize right after mouseup would have wrongly succeeded.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g29">
        <table id="tbl29">
          <thead><tr>
            <th id="th29a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th id="th29b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:120px">
              <span data-reorder-grip>::</span>B<span id="handle29b" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g29', window.__fakeDotNet);
    window.__C.registerColumnResize('handle29b', window.__fakeDotNet, 50, 500);
  });
  const grip29 = await page.$('div[data-grid-id="g29"] th[data-col-id="A"] [data-reorder-grip]');
  const grip29Box = await grip29.boundingBox();
  const th29b = await page.$('#th29b');
  const th29bBox = await th29b.boundingBox();
  await page.mouse.move(grip29Box.x + grip29Box.width / 2, grip29Box.y + grip29Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th29bBox.x + th29bBox.width / 2, th29bBox.y + th29bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  const blockedRightAfterDrop29 = await page.evaluate(() => {
    const handle = document.getElementById('handle29b');
    const th = document.getElementById('th29b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 9, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 9, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { resizing: handle.dataset.resizing, width: th.style.width };
  });
  assert(blockedRightAfterDrop29.resizing === undefined, `resize is blocked immediately after drop, while the commit is still settling (data-resizing got '${blockedRightAfterDrop29.resizing}')`);
  // Let the settle timeout actually fire — no unregister this time.
  await page.waitForTimeout(300);
  const afterSettle29 = await page.evaluate(() => ({
    commits: window.__dotnetCalls.filter((c) => c.method === 'OnColumnReorderCommit' && c.args[0] === 'g29').length,
  }));
  assert(afterSettle29.commits === 1, `the settle timeout fires the queued column commit exactly once (got ${afterSettle29.commits})`);
  const resizing29 = await page.evaluate(() => {
    const handle = document.getElementById('handle29b');
    const th = document.getElementById('th29b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 11, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 11, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return handle.dataset.resizing;
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const freedAfterSettle29 = await page.evaluate(() => {
    const handle = document.getElementById('handle29b');
    const th = document.getElementById('th29b');
    const rect = th.getBoundingClientRect();
    const width = th.style.width;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 11, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { width };
  });
  assert(resizing29 === 'true', `resize succeeds once the settle timeout has dispatched the commit and released the arbiter (data-resizing got '${resizing29}')`);
  assert(freedAfterSettle29.width === '185px', `resize actually applied once the settle-released arbiter allowed it (got '${freedAfterSettle29.width}')`);

  // ---------------------------------------------------------------
  // TEST 30 — Row reorder, NATURAL settle completion (round-10 #3, vertical
  // mirror of TEST 29).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g30">
        <table>
          <thead><tr>
            <th id="th30" data-col-id="rc" style="width:100px;min-width:100px">
              <span id="handle30" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody>
          <tr data-row-index="0" data-row-key="r0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="r1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g30', window.__fakeDotNet);
    window.__C.registerColumnResize('handle30', window.__fakeDotNet, 50, 500);
  });
  const grip30 = await page.$('div[data-grid-id="g30"] tr[data-row-key="r0"] [data-row-reorder-grip]');
  const grip30Box = await grip30.boundingBox();
  const r1Row30 = await page.$('div[data-grid-id="g30"] tr[data-row-key="r1"]');
  const r1Row30Box = await r1Row30.boundingBox();
  await page.mouse.move(grip30Box.x + grip30Box.width / 2, grip30Box.y + grip30Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1Row30Box.x + r1Row30Box.width / 2, r1Row30Box.y + r1Row30Box.height / 2, { steps: 5 });
  await page.mouse.up();
  const blockedRightAfterDrop30 = await page.evaluate(() => {
    const handle = document.getElementById('handle30');
    const th = document.getElementById('th30');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 9, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 9, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { resizing: handle.dataset.resizing, width: th.style.width };
  });
  assert(blockedRightAfterDrop30.resizing === undefined, `resize is blocked immediately after drop, while the row commit is still settling (data-resizing got '${blockedRightAfterDrop30.resizing}')`);
  await page.waitForTimeout(300);
  const afterSettle30 = await page.evaluate(() => ({
    commits: window.__dotnetCalls.filter((c) => c.method === 'OnRowReorderCommit' && c.args[0] === 'g30').length,
  }));
  assert(afterSettle30.commits === 1, `the settle timeout fires the queued row commit exactly once (got ${afterSettle30.commits})`);
  const resizing30 = await page.evaluate(() => {
    const handle = document.getElementById('handle30');
    const th = document.getElementById('th30');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 11, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 11, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return handle.dataset.resizing;
  });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const freedAfterSettle30 = await page.evaluate(() => {
    const handle = document.getElementById('handle30');
    const th = document.getElementById('th30');
    const rect = th.getBoundingClientRect();
    const width = th.style.width;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 11, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { width };
  });
  assert(resizing30 === 'true', `resize succeeds once the row settle timeout has dispatched the commit and released the arbiter (data-resizing got '${resizing30}')`);
  assert(freedAfterSettle30.width === '165px', `resize actually applied once the settle-released arbiter allowed it (got '${freedAfterSettle30.width}')`);

  // ---------------------------------------------------------------
  // TEST 31 — clearColumnReorderTransforms finds settle-transformed cells by
  // ELEMENT REFERENCE, not by a data-col-id selector, so a rejecting
  // rerender that strips that attribute BEFORE this cleanup runs still gets
  // the JS-authored transforms cleared (round-11 #1).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g31">
        <table id="tbl31">
          <thead><tr>
            <th id="th31a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th id="th31b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px">
              <span data-reorder-grip>::</span>B</th>
          </tr></thead>
          <tbody><tr><td id="td31a">a</td><td id="td31b">b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g31', window.__fakeDotNet);
  });
  const grip31 = await page.$('div[data-grid-id="g31"] th[data-col-id="A"] [data-reorder-grip]');
  const grip31Box = await grip31.boundingBox();
  const th31b = await page.$('#th31b');
  const th31bBox = await th31b.boundingBox();
  await page.mouse.move(grip31Box.x + grip31Box.width / 2, grip31Box.y + grip31Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th31bBox.x + th31bBox.width / 2, th31bBox.y + th31bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Let the settle timeout fire naturally — the fake dotNet just records the
  // OnColumnReorderCommit call (it never calls back CaptureColumnRects), so
  // the JS-authored settle transforms stay applied on th31a/th31b + their
  // body cells exactly as they would while the real .NET side is still
  // deciding accept vs reject.
  await page.waitForTimeout(300);
  const midSettle31 = await page.evaluate(() => ({
    aTransform: document.getElementById('th31a').style.transform,
    bTransform: document.getElementById('th31b').style.transform,
  }));
  assert(midSettle31.aTransform !== '', `dragged column A still carries its settled transform before the reject cleanup runs (got '${midSettle31.aTransform}')`);
  assert(midSettle31.bTransform !== '', `shifted sibling B still carries its live-shift transform before the reject cleanup runs (got '${midSettle31.bTransform}')`);

  // Simulate the REJECTING rerender (Reorderable toggled off / grouped /
  // ColumnVirtualize toggled) stripping data-col-id from these exact th's
  // BEFORE ClearColumnReorderTransforms runs — an attribute-based sweep
  // alone would now match nothing.
  await page.evaluate(() => {
    document.getElementById('th31a').removeAttribute('data-col-id');
    document.getElementById('th31b').removeAttribute('data-col-id');
  });
  await page.evaluate(() => window.__C.clearColumnReorderTransforms('g31'));
  const afterReject31 = await page.evaluate(() => ({
    aTransform: document.getElementById('th31a').style.transform,
    bTransform: document.getElementById('th31b').style.transform,
    tdA: document.getElementById('td31a').style.transform,
    tdB: document.getElementById('td31b').style.transform,
  }));
  assert(afterReject31.aTransform === '', `clearColumnReorderTransforms clears A's th transform even after data-col-id was stripped (got '${afterReject31.aTransform}')`);
  assert(afterReject31.bTransform === '', `clearColumnReorderTransforms clears B's th transform even after data-col-id was stripped (got '${afterReject31.bTransform}')`);
  assert(afterReject31.tdA === '', `clearColumnReorderTransforms also clears A's body cell transform by reference (got '${afterReject31.tdA}')`);
  assert(afterReject31.tdB === '', `clearColumnReorderTransforms also clears B's body cell transform by reference (got '${afterReject31.tdB}')`);

  // ---------------------------------------------------------------
  // TEST 32 — Row-engine mirror of TEST 31: clearRowReorderTransforms finds
  // settle-transformed rows by element reference, immune to a rejecting
  // rerender stripping data-row-index first (round-11 #1).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g32">
        <table>
          <thead><tr><th data-col-id="rc">Col</th></tr></thead>
          <tbody>
          <tr id="tr32r0" data-row-index="0" data-row-key="r0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr id="tr32r1" data-row-index="1" data-row-key="r1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g32', window.__fakeDotNet);
  });
  const grip32 = await page.$('div[data-grid-id="g32"] tr[data-row-key="r0"] [data-row-reorder-grip]');
  const grip32Box = await grip32.boundingBox();
  const r1Row32 = await page.$('div[data-grid-id="g32"] tr[data-row-key="r1"]');
  const r1Row32Box = await r1Row32.boundingBox();
  await page.mouse.move(grip32Box.x + grip32Box.width / 2, grip32Box.y + grip32Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1Row32Box.x + r1Row32Box.width / 2, r1Row32Box.y + r1Row32Box.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const midSettle32 = await page.evaluate(() => ({
    r0Transform: document.getElementById('tr32r0').style.transform,
    r1Transform: document.getElementById('tr32r1').style.transform,
  }));
  assert(midSettle32.r0Transform !== '', `dragged row R0 still carries its settled transform before the reject cleanup runs (got '${midSettle32.r0Transform}')`);
  assert(midSettle32.r1Transform !== '', `shifted sibling R1 still carries its live-shift transform before the reject cleanup runs (got '${midSettle32.r1Transform}')`);

  // Simulate the rejecting rerender stripping data-row-index from BOTH rows
  // before ClearRowReorderTransforms runs.
  await page.evaluate(() => {
    document.getElementById('tr32r0').removeAttribute('data-row-index');
    document.getElementById('tr32r1').removeAttribute('data-row-index');
  });
  await page.evaluate(() => window.__C.clearRowReorderTransforms('g32'));
  const afterReject32 = await page.evaluate(() => ({
    r0Transform: document.getElementById('tr32r0').style.transform,
    r1Transform: document.getElementById('tr32r1').style.transform,
  }));
  assert(afterReject32.r0Transform === '', `clearRowReorderTransforms clears R0's transform even after data-row-index was stripped (got '${afterReject32.r0Transform}')`);
  assert(afterReject32.r1Transform === '', `clearRowReorderTransforms clears R1's transform even after data-row-index was stripped (got '${afterReject32.r1Transform}')`);

  // ---------------------------------------------------------------
  // TEST 33 — Double-click auto-fit is gated on the arbiter (round-11 #2): a
  // column-reorder commit still settling on this grid must refuse a
  // dblclick auto-fit on a DIFFERENT column's resize handle — no width
  // mutation, no OnColumnResizeCommit — exactly like a pointerdown would be
  // refused. Once the settle window ends, the SAME dblclick succeeds.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g33">
        <table id="tbl33">
          <thead><tr>
            <th id="th33a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
              <span data-reorder-grip>::</span>A</th>
            <th id="th33b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:60px">
              <span data-reorder-grip>::</span><span>bb</span><span id="handle33b" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g33', window.__fakeDotNet);
    window.__C.registerColumnResize('handle33b', window.__fakeDotNet, 50, 500);
  });
  const grip33 = await page.$('div[data-grid-id="g33"] th[data-col-id="A"] [data-reorder-grip]');
  const grip33Box = await grip33.boundingBox();
  const th33b = await page.$('#th33b');
  const th33bBox = await th33b.boundingBox();
  await page.mouse.move(grip33Box.x + grip33Box.width / 2, grip33Box.y + grip33Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th33bBox.x + th33bBox.width / 2, th33bBox.y + th33bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Still inside the ~200ms settle window — attempt the auto-fit dblclick.
  const blockedDblClick33 = await page.evaluate(() => {
    const handle = document.getElementById('handle33b');
    const th = document.getElementById('th33b');
    handle.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
    return { width: th.style.width };
  });
  assert(blockedDblClick33.width === '120px', `auto-fit dblclick refused while a column-reorder commit is still settling — width untouched at 120px (got '${blockedDblClick33.width}')`);
  const blockedCommits33 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(blockedCommits33.length === 0, `no OnColumnResizeCommit fires for the refused auto-fit dblclick (got ${blockedCommits33.length})`);

  // Let the settle timeout fire and release the arbiter — the same dblclick
  // now succeeds normally.
  await page.waitForTimeout(300);
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const handle = document.getElementById('handle33b');
    handle.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
  });
  const freedCommits33 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(freedCommits33.length === 1, `auto-fit dblclick succeeds once the column-reorder settle window has ended (got ${freedCommits33.length})`);
  assert(freedCommits33[0].args[2] === true, `the freed dblclick's commit is the real auto-fit one (autoFit=true) (got ${JSON.stringify(freedCommits33[0].args)})`);

  // ---------------------------------------------------------------
  // TEST 34 — Row-engine mirror of TEST 33: a row-reorder commit still
  // settling on this grid must refuse a dblclick auto-fit resize on the
  // grid's own resize handle too (round-11 #2).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g34">
        <table>
          <thead><tr>
            <th id="th34" data-col-id="rc" style="width:100px;min-width:50px">
              <span>col</span><span id="handle34" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody>
          <tr data-row-index="0" data-row-key="r0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="r1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerRowReorder('g34', window.__fakeDotNet);
    window.__C.registerColumnResize('handle34', window.__fakeDotNet, 50, 500);
  });
  const grip34 = await page.$('div[data-grid-id="g34"] tr[data-row-key="r0"] [data-row-reorder-grip]');
  const grip34Box = await grip34.boundingBox();
  const r1Row34 = await page.$('div[data-grid-id="g34"] tr[data-row-key="r1"]');
  const r1Row34Box = await r1Row34.boundingBox();
  await page.mouse.move(grip34Box.x + grip34Box.width / 2, grip34Box.y + grip34Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1Row34Box.x + r1Row34Box.width / 2, r1Row34Box.y + r1Row34Box.height / 2, { steps: 5 });
  await page.mouse.up();
  const blockedDblClick34 = await page.evaluate(() => {
    const handle = document.getElementById('handle34');
    const th = document.getElementById('th34');
    handle.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
    return { width: th.style.width };
  });
  assert(blockedDblClick34.width === '100px', `auto-fit dblclick refused while a row-reorder commit is still settling — width untouched at 100px (got '${blockedDblClick34.width}')`);
  const blockedCommits34 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(blockedCommits34.length === 0, `no OnColumnResizeCommit fires for the refused auto-fit dblclick during a row-reorder settle (got ${blockedCommits34.length})`);

  await page.waitForTimeout(300);
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const handle = document.getElementById('handle34');
    handle.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
  });
  const freedCommits34 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(freedCommits34.length === 1, `auto-fit dblclick succeeds once the row-reorder settle window has ended (got ${freedCommits34.length})`);

  // ---------------------------------------------------------------
  // TEST 35 — Right-click (non-primary mouse button) on the column-reorder
  // GRIP must not claim the arbiter, must not arm the drag, and must not
  // preventDefault the pointerdown (which would suppress the grip's context
  // menu) — round-12 #1.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g35">
        <table><thead><tr>
          <th id="th35a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip id="grip35a">::</span>A</th>
          <th id="th35b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip id="grip35b">::</span>B</th>
        </tr></thead>
        <tbody><tr><td>a</td><td>b</td></tr></tbody></table>
      </div>`;
  });
  const r35 = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g35', window.__fakeDotNet);
    const gripA = document.getElementById('grip35a');
    const thA = document.getElementById('th35a');
    // button: 2 = right mouse button. dispatchEvent returns FALSE only if
    // preventDefault() was called on this exact event by a listener.
    const notPrevented = gripA.dispatchEvent(new PointerEvent('pointerdown', {
      pointerId: 1, clientX: 5, clientY: 5, bubbles: true, cancelable: true, button: 2, pointerType: 'mouse',
    }));
    const armedOpacity = thA.style.opacity;
    // The arbiter token must still be free — a genuine LEFT-button grip
    // press on the OTHER column must arm immediately.
    const gripB = document.getElementById('grip35b');
    const thB = document.getElementById('th35b');
    gripB.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 2, clientX: 5, clientY: 5, bubbles: true, cancelable: true, button: 0 }));
    const armedOpacityB = thB.style.opacity;
    gripB.dispatchEvent(new PointerEvent('pointerup', { pointerId: 2, clientX: 5, clientY: 5, bubbles: true, cancelable: true }));
    return { notPrevented, armedOpacity, armedOpacityB };
  });
  assert(r35.notPrevented === true, `right-click on the reorder grip does not preventDefault the pointerdown — its context menu stays available (got ${r35.notPrevented})`);
  assert(r35.armedOpacity === '', `right-click on the reorder grip never arms a drag (opacity got '${r35.armedOpacity}')`);
  assert(r35.armedOpacityB === '0.8', `the arbiter token is still free after the right-click — a real left-button grip press on another column succeeds (opacity got '${r35.armedOpacityB}')`);
  await page.waitForTimeout(220); // let B's own (armed, no-move) cancel settle window elapse

  // ---------------------------------------------------------------
  // TEST 36 — The arbiter token is held for the FULL LIFETIME of the commit
  // interop promise (round-12 #2), not released synchronously right after
  // invokeMethodAsync merely STARTS: a resize on the same grid is refused
  // while a deliberately slow OnColumnReorderCommit promise is still
  // pending, and only succeeds once that promise actually RESOLVES.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g36">
        <table><thead><tr>
          <th id="th36a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip>::</span>A</th>
          <th id="th36b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:120px">
            <span data-reorder-grip>::</span>B<span id="handle36b" data-slot="datagrid-resize-handle"></span></th>
        </tr></thead>
        <tbody><tr><td>a</td><td>b</td></tr></tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const dotnet = window.__makeDeferredDotNet('d36');
    window.__C.registerColumnReorder('g36', dotnet);
    window.__C.registerColumnResize('handle36b', dotnet, 50, 500);
  });
  const grip36 = await page.$('div[data-grid-id="g36"] th[data-col-id="A"] [data-reorder-grip]');
  const grip36Box = await grip36.boundingBox();
  const th36b = await page.$('#th36b');
  const th36bBox = await th36b.boundingBox();
  await page.mouse.move(grip36Box.x + grip36Box.width / 2, grip36Box.y + grip36Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th36bBox.x + th36bBox.width / 2, th36bBox.y + th36bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Let the ~200ms settle timeout fire — invokeMethodAsync('OnColumnReorderCommit')
  // is STARTED here but the deferred promise stays pending until resolved below.
  await page.waitForTimeout(250);
  const midCommit36 = await page.evaluate(() => {
    const started = window.__dotnetCalls.filter((c) => c.method === 'OnColumnReorderCommit').length;
    const pending = window.__deferred['d36'].pendingCount();
    const handle = document.getElementById('handle36b');
    const th = document.getElementById('th36b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 20, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 20, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 20, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { started, pending, resizing };
  });
  assert(midCommit36.started === 1, `the settle timeout fires and STARTS the OnColumnReorderCommit interop call (got ${midCommit36.started})`);
  assert(midCommit36.pending === 1, `the commit promise is deliberately left pending (got ${midCommit36.pending})`);
  assert(midCommit36.resizing === undefined, `resize is refused while the slow commit promise is still PENDING — arbiter token still held (data-resizing got '${midCommit36.resizing}')`);
  // Resolve the slow commit — its `finally` now releases the token.
  await page.evaluate(() => window.__deferred['d36'].resolveNext());
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const afterResolve36 = await page.evaluate(() => {
    const handle = document.getElementById('handle36b');
    const th = document.getElementById('th36b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 21, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 21, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 21, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return resizing;
  });
  assert(afterResolve36 === 'true', `resize succeeds once the slow commit promise RESOLVES and its \`finally\` releases the arbiter (data-resizing got '${afterResolve36}')`);

  // ---------------------------------------------------------------
  // TEST 37 — Mirror of TEST 36: the arbiter token is released from the
  // commit interop's `finally` even when the promise REJECTS — a failed
  // .NET round-trip must never strand the token and permanently block the
  // grid (round-12 #2).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g37">
        <table><thead><tr>
          <th id="th37a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip>::</span>A</th>
          <th id="th37b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:120px">
            <span data-reorder-grip>::</span>B<span id="handle37b" data-slot="datagrid-resize-handle"></span></th>
        </tr></thead>
        <tbody><tr><td>a</td><td>b</td></tr></tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const dotnet = window.__makeDeferredDotNet('d37');
    window.__C.registerColumnReorder('g37', dotnet);
    window.__C.registerColumnResize('handle37b', dotnet, 50, 500);
  });
  const grip37 = await page.$('div[data-grid-id="g37"] th[data-col-id="A"] [data-reorder-grip]');
  const grip37Box = await grip37.boundingBox();
  const th37b = await page.$('#th37b');
  const th37bBox = await th37b.boundingBox();
  await page.mouse.move(grip37Box.x + grip37Box.width / 2, grip37Box.y + grip37Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th37bBox.x + th37bBox.width / 2, th37bBox.y + th37bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(250);
  const midReject37 = await page.evaluate(() => {
    const pending = window.__deferred['d37'].pendingCount();
    const handle = document.getElementById('handle37b');
    const th = document.getElementById('th37b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 30, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 30, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 30, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return { pending, resizing };
  });
  assert(midReject37.pending === 1, `the commit promise is deliberately left pending before rejecting (got ${midReject37.pending})`);
  assert(midReject37.resizing === undefined, `resize is refused while the (about to fail) commit promise is still pending (data-resizing got '${midReject37.resizing}')`);
  // Reject the slow commit — its `finally` still releases the token.
  await page.evaluate(() => window.__deferred['d37'].rejectNext(new Error('simulated interop failure')));
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const afterReject37 = await page.evaluate(() => {
    const handle = document.getElementById('handle37b');
    const th = document.getElementById('th37b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 31, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 31, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 31, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    return resizing;
  });
  assert(afterReject37 === 'true', `resize succeeds once the slow commit promise REJECTS — its \`finally\` still released the arbiter (data-resizing got '${afterReject37}')`);

  // ---------------------------------------------------------------
  // TEST 38 — Keyboard resize (nudgeColumnResize) is gated by the arbiter
  // (round-12 #3): a keyboard nudge on a column is refused (silently, no
  // width mutation, no commit) while a column-reorder commit is still in
  // its post-release settle window, and succeeds once that window ends.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g38">
        <table><thead><tr>
          <th id="th38a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip>::</span>A</th>
          <th id="th38b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:60px">
            <span data-reorder-grip>::</span>B<span id="handle38b" data-slot="datagrid-resize-handle"></span></th>
        </tr></thead>
        <tbody><tr><td>a</td><td>b</td></tr></tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g38', window.__fakeDotNet);
    window.__C.registerColumnResize('handle38b', window.__fakeDotNet, 50, 500);
  });
  const grip38 = await page.$('div[data-grid-id="g38"] th[data-col-id="A"] [data-reorder-grip]');
  const grip38Box = await grip38.boundingBox();
  const th38b = await page.$('#th38b');
  const th38bBox = await th38b.boundingBox();
  await page.mouse.move(grip38Box.x + grip38Box.width / 2, grip38Box.y + grip38Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th38bBox.x + th38bBox.width / 2, th38bBox.y + th38bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Still inside the ~200ms settle window — attempt a keyboard nudge on column B.
  const blockedNudge38 = await page.evaluate(() => {
    const th = document.getElementById('th38b');
    const before = th.style.width;
    const ret = window.__C.nudgeColumnResize('handle38b', 10);
    return { before, after: th.style.width, ret };
  });
  assert(blockedNudge38.before === blockedNudge38.after, `keyboard nudge refused while a column-reorder commit is still settling — width untouched (before '${blockedNudge38.before}', after '${blockedNudge38.after}')`);
  const blockedCommits38 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(blockedCommits38.length === 0, `no OnColumnResizeCommit fires for the refused keyboard nudge (got ${blockedCommits38.length})`);

  await page.waitForTimeout(300);
  const freedNudge38 = await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const ret = window.__C.nudgeColumnResize('handle38b', 10);
    const th = document.getElementById('th38b');
    return { ret, width: th.style.width };
  });
  assert(freedNudge38.ret === 130, `keyboard nudge succeeds once the column-reorder settle window has ended, returning the new width (got ${freedNudge38.ret})`);
  assert(freedNudge38.width === '130px', `keyboard nudge actually applied the width once the arbiter freed up (got '${freedNudge38.width}')`);
  const freedCommits38 = await page.evaluate(() => window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit'));
  assert(freedCommits38.length === 1, `keyboard nudge commits exactly once after being freed (got ${freedCommits38.length})`);

  // ---------------------------------------------------------------
  // TEST 39 — Column reorder: unregister DURING the IN-FLIGHT COMMIT window
  // (the settle timeout already fired and STARTED invokeMethodAsync, but the
  // promise hasn't settled yet) leaves NO stranded transforms (round-13 #4).
  // Before the fix, the settle timeout nulled pendingSettle synchronously the
  // instant it fired — before invokeMethodAsync even started — so
  // unregisterColumnReorder's cancelActiveDrag saw pendingSettle === null and
  // skipped its cleanup entirely, even though columnReorderSettleEls still
  // held the dragged/shifted cells: nothing would ever strip their
  // transforms (no future captureColumnRects/clearColumnReorderTransforms
  // call was coming for a torn-down component), and the arbiter token would
  // leak until an in-flight `finally` that no longer matters to anyone.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g39">
        <table><thead><tr>
          <th id="th39a" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:100px">
            <span data-reorder-grip>::</span>A</th>
          <th id="th39b" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:120px;min-width:120px">
            <span data-reorder-grip>::</span>B<span id="handle39b" data-slot="datagrid-resize-handle"></span></th>
        </tr></thead>
        <tbody><tr><td>a</td><td>b</td></tr></tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const dotnet = window.__makeDeferredDotNet('d39');
    window.__C.registerColumnReorder('g39', dotnet);
    window.__C.registerColumnResize('handle39b', dotnet, 50, 500);
  });
  const grip39 = await page.$('div[data-grid-id="g39"] th[data-col-id="A"] [data-reorder-grip]');
  const grip39Box = await grip39.boundingBox();
  const th39b = await page.$('#th39b');
  const th39bBox = await th39b.boundingBox();
  await page.mouse.move(grip39Box.x + grip39Box.width / 2, grip39Box.y + grip39Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(th39bBox.x + th39bBox.width / 2, th39bBox.y + th39bBox.height / 2, { steps: 5 });
  await page.mouse.up();
  // Let the ~200ms settle timeout fire — invokeMethodAsync('OnColumnReorderCommit')
  // STARTS here but the deferred promise stays pending (never resolved in this test).
  await page.waitForTimeout(250);
  const midFlight39 = await page.evaluate(() => ({
    started: window.__dotnetCalls.filter((c) => c.method === 'OnColumnReorderCommit').length,
    pending: window.__deferred['d39'].pendingCount(),
    aTransform: document.getElementById('th39a').style.transform,
    bTransform: document.getElementById('th39b').style.transform,
  }));
  assert(midFlight39.started === 1, `settle timeout fires and STARTS the commit interop call (got ${midFlight39.started})`);
  assert(midFlight39.pending === 1, `commit promise is deliberately left pending (got ${midFlight39.pending})`);
  assert(midFlight39.aTransform !== '', `dragged column A still carries its settled transform mid-commit (got '${midFlight39.aTransform}')`);
  assert(midFlight39.bTransform !== '', `shifted sibling B still carries its settled transform mid-commit (got '${midFlight39.bTransform}')`);

  // Unregister WHILE the commit is still in flight — the grid tears down, but
  // the promise above is still pending and may settle (or not) afterward.
  await page.evaluate(() => window.__C.unregisterColumnReorder('g39'));
  const afterUnregister39 = await page.evaluate(() => ({
    aTransform: document.getElementById('th39a').style.transform,
    bTransform: document.getElementById('th39b').style.transform,
  }));
  assert(afterUnregister39.aTransform === '', `unregister during the in-flight commit strips column A's transform (got '${afterUnregister39.aTransform}')`);
  assert(afterUnregister39.bTransform === '', `unregister during the in-flight commit strips column B's transform (got '${afterUnregister39.bTransform}')`);

  // The arbiter token must also be freed immediately, not leaked until the
  // (now-irrelevant) in-flight promise eventually settles.
  const freedArbiter39 = await page.evaluate(() => {
    const handle = document.getElementById('handle39b');
    const th = document.getElementById('th39b');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 40, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 40, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointercancel', { pointerId: 40, bubbles: true, cancelable: true }));
    return resizing;
  });
  assert(freedArbiter39 === 'true', `arbiter token is freed immediately by unregister-during-in-flight-commit (data-resizing got '${freedArbiter39}')`);

  // Resolving the now-stale in-flight promise afterward must not throw or
  // double-strand anything (releaseGridDrag's owner check makes this a
  // harmless no-op once the grid has torn down / been reclaimed).
  await page.evaluate(() => window.__deferred['d39'].resolveNext());

  // ---------------------------------------------------------------
  // TEST 40 — Row reorder: unregister DURING the in-flight commit window
  // leaves NO stranded transforms (round-13 #4, vertical mirror of TEST 39).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g40">
        <table>
          <thead><tr>
            <th id="th40" data-col-id="rc" style="width:100px;min-width:100px">
              <span id="handle40" data-slot="datagrid-resize-handle"></span></th>
          </tr></thead>
          <tbody>
          <tr data-row-index="0" data-row-key="r0"><td><span data-row-reorder-grip>::</span>R0</td></tr>
          <tr data-row-index="1" data-row-key="r1"><td><span data-row-reorder-grip>::</span>R1</td></tr>
        </tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const dotnet = window.__makeDeferredDotNet('d40');
    window.__C.registerRowReorder('g40', dotnet);
    window.__C.registerColumnResize('handle40', dotnet, 50, 500);
  });
  const grip40 = await page.$('div[data-grid-id="g40"] tr[data-row-key="r0"] [data-row-reorder-grip]');
  const grip40Box = await grip40.boundingBox();
  const r1Row40 = await page.$('div[data-grid-id="g40"] tr[data-row-key="r1"]');
  const r1Row40Box = await r1Row40.boundingBox();
  await page.mouse.move(grip40Box.x + grip40Box.width / 2, grip40Box.y + grip40Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(r1Row40Box.x + r1Row40Box.width / 2, r1Row40Box.y + r1Row40Box.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(250);
  const midFlight40 = await page.evaluate(() => ({
    started: window.__dotnetCalls.filter((c) => c.method === 'OnRowReorderCommit').length,
    pending: window.__deferred['d40'].pendingCount(),
    r0Transform: document.querySelector('div[data-grid-id="g40"] tr[data-row-key="r0"]').style.transform,
    r1Transform: document.querySelector('div[data-grid-id="g40"] tr[data-row-key="r1"]').style.transform,
  }));
  assert(midFlight40.started === 1, `settle timeout fires and STARTS the row commit interop call (got ${midFlight40.started})`);
  assert(midFlight40.pending === 1, `row commit promise is deliberately left pending (got ${midFlight40.pending})`);
  assert(midFlight40.r0Transform !== '', `dragged row R0 still carries its settled transform mid-commit (got '${midFlight40.r0Transform}')`);
  assert(midFlight40.r1Transform !== '', `shifted sibling R1 still carries its settled transform mid-commit (got '${midFlight40.r1Transform}')`);

  await page.evaluate(() => window.__C.unregisterRowReorder('g40'));
  const afterUnregister40 = await page.evaluate(() => ({
    r0Transform: document.querySelector('div[data-grid-id="g40"] tr[data-row-key="r0"]').style.transform,
    r1Transform: document.querySelector('div[data-grid-id="g40"] tr[data-row-key="r1"]').style.transform,
  }));
  assert(afterUnregister40.r0Transform === '', `unregister during the in-flight commit strips row R0's transform (got '${afterUnregister40.r0Transform}')`);
  assert(afterUnregister40.r1Transform === '', `unregister during the in-flight commit strips row R1's transform (got '${afterUnregister40.r1Transform}')`);

  const freedArbiter40 = await page.evaluate(() => {
    const handle = document.getElementById('handle40');
    const th = document.getElementById('th40');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 41, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 41, clientX: rect.right + 40, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointercancel', { pointerId: 41, bubbles: true, cancelable: true }));
    return resizing;
  });
  assert(freedArbiter40 === 'true', `arbiter token is freed immediately by unregister-during-in-flight row commit (data-resizing got '${freedArbiter40}')`);

  await page.evaluate(() => window.__deferred['d40'].resolveNext());

  // ---------------------------------------------------------------
  // TEST 41 — Resize: a SECOND resize gesture on the SAME handle is refused
  // end-to-end while an EARLIER commit on that same handle is still in
  // flight (a slow persistence handler), and only succeeds once that commit
  // settles (round-13 #1, proven through the JS arbiter — same-handle/
  // same-engine mirror of TEST 36's cross-engine coverage).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g41">
        <table><thead><tr>
          <th id="th41" data-col-id="c41" style="width:100px;min-width:50px">
            Col<span id="handle41" data-slot="datagrid-resize-handle"></span>
          </th>
        </tr></thead><tbody><tr><td>cell</td></tr></tbody></table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    const dotnet = window.__makeDeferredDotNet('d41');
    window.__C.registerColumnResize('handle41', dotnet, 50, 500);
  });
  // First drag: commit a width change — its interop call is left pending.
  // Dispatched directly on the handle (mirrors TEST 1/27/etc.) rather than via
  // real page.mouse coordinates, since the harness's plain-flow CSS doesn't
  // position the handle at a predictable on-screen offset for real hit-testing.
  await page.evaluate(() => {
    const handle = document.getElementById('handle41');
    const th = document.getElementById('th41');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 49, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 49, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
  });
  // The live width write is rAF-throttled — wait a frame before releasing
  // (mirrors TEST 1's own rAF wait) so the commit reflects the dragged width.
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  await page.evaluate(() => {
    const handle = document.getElementById('handle41');
    const th = document.getElementById('th41');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerup', { pointerId: 49, clientX: rect.right + 60, clientY: rect.top + 5, bubbles: true, cancelable: true }));
  });
  const midCommit41 = await page.evaluate(() => ({
    started: window.__dotnetCalls.filter((c) => c.method === 'OnColumnResizeCommit').length,
    pending: window.__deferred['d41'].pendingCount(),
  }));
  assert(midCommit41.started === 1, `first resize commit interop call started (got ${midCommit41.started})`);
  assert(midCommit41.pending === 1, `first resize commit promise deliberately left pending (got ${midCommit41.pending})`);

  // A SECOND drag on the SAME handle, attempted while the first commit is
  // still in flight, must be refused by the arbiter — the width is left
  // completely untouched by the blocked attempt.
  const blocked41 = await page.evaluate(() => {
    const handle = document.getElementById('handle41');
    const th = document.getElementById('th41');
    const rect = th.getBoundingClientRect();
    const widthBefore = th.style.width;
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 50, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 50, clientX: rect.right + 30, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointercancel', { pointerId: 50, bubbles: true, cancelable: true }));
    return { resizing, widthBefore, widthAfter: th.style.width };
  });
  assert(blocked41.resizing === undefined, `second resize on the SAME handle is refused while the first commit is still in flight (data-resizing got '${blocked41.resizing}')`);
  assert(blocked41.widthBefore === blocked41.widthAfter, `blocked second resize never applied a live width (before '${blocked41.widthBefore}', after '${blocked41.widthAfter}')`);

  // Resolve the first (slow) commit — its `finally` releases the token.
  await page.evaluate(() => window.__deferred['d41'].resolveNext());
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  const freed41 = await page.evaluate(() => {
    const handle = document.getElementById('handle41');
    const th = document.getElementById('th41');
    const rect = th.getBoundingClientRect();
    handle.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 51, clientX: rect.right - 5, clientY: rect.top + 5, bubbles: true, cancelable: true, button: 0 }));
    handle.dispatchEvent(new PointerEvent('pointermove', { pointerId: 51, clientX: rect.right + 30, clientY: rect.top + 5, bubbles: true, cancelable: true }));
    const resizing = handle.dataset.resizing;
    handle.dispatchEvent(new PointerEvent('pointercancel', { pointerId: 51, bubbles: true, cancelable: true }));
    return resizing;
  });
  assert(freed41 === 'true', `resize on the SAME handle succeeds once the earlier commit's promise resolves and frees the arbiter (data-resizing got '${freed41}')`);

  // ---------------------------------------------------------------
  // TEST 42 — Header-drag redesign (a): a plain click on the title (the
  // data-slot="datagrid-sort-button" zone-B surface) reaches the button —
  // no movement, no drag armed, no reorder commit. There's no Blazor host in
  // this harness, so "sorts" is observed as the native click actually
  // reaching the button (a plain click listener), which is what HandleSort
  // is wired to in the real component.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g42">
        <table id="tbl42" style="width:300px">
          <thead><tr>
            <th id="th42A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn42A" data-slot="datagrid-sort-button" style="width:100%;height:100%;">A</button></th>
            <th id="th42B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn42B" data-slot="datagrid-sort-button" style="width:100%;height:100%;">B</button></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
    window.__sortClicks42 = 0;
    document.getElementById('btn42A').addEventListener('click', () => { window.__sortClicks42++; });
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g42', window.__fakeDotNet);
  });
  const btn42A = await page.$('#btn42A');
  const box42A = await btn42A.boundingBox();
  await page.mouse.move(box42A.x + box42A.width / 2, box42A.y + box42A.height / 2);
  await page.mouse.down();
  await page.mouse.up();
  await page.waitForTimeout(50);
  const result42 = await page.evaluate(() => ({
    clicks: window.__sortClicks42,
    commits: window.__dotnetCalls.filter((c) => c.method === 'OnColumnReorderCommit').length,
    opacity: document.getElementById('th42A').style.opacity,
  }));
  assert(result42.clicks === 1, `a plain click (no movement) on the sort button reaches it exactly once (got ${result42.clicks})`);
  assert(result42.commits === 0, `a plain click never fires a reorder commit (got ${result42.commits})`);
  assert(result42.opacity === '', `a plain click never arms the drag lift (header opacity got '${result42.opacity}')`);

  // ---------------------------------------------------------------
  // TEST 43 — Header-drag redesign (b): a 6px mouse drag starting on the
  // title (data-slot="datagrid-sort-button") — past REORDER_MOVE_THRESHOLD
  // (5px) — arms a reorder and, after release over a different column,
  // commits it WITHOUT ever firing the sort button's click (click-
  // suppression, redesign point 2).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g43">
        <table id="tbl43" style="width:300px">
          <thead><tr>
            <th id="th43A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn43A" data-slot="datagrid-sort-button" style="width:100%;height:100%;">A</button></th>
            <th id="th43B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn43B" data-slot="datagrid-sort-button" style="width:100%;height:100%;">B</button></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
    window.__sortClicks43 = 0;
    document.getElementById('btn43A').addEventListener('click', () => { window.__sortClicks43++; });
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g43', window.__fakeDotNet);
  });
  const btn43A = await page.$('#btn43A');
  const box43A = await btn43A.boundingBox();
  const th43B = await page.$('#th43B');
  const box43B = await th43B.boundingBox();
  await page.mouse.move(box43A.x + box43A.width / 2, box43A.y + box43A.height / 2);
  await page.mouse.down();
  await page.mouse.move(box43A.x + box43A.width / 2 + 6, box43A.y + box43A.height / 2, { steps: 2 });
  const armedOpacity43 = await page.evaluate(() => document.getElementById('th43A').style.opacity);
  await page.mouse.move(box43B.x + box43B.width / 2, box43B.y + box43B.height / 2, { steps: 5 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  const result43 = await page.evaluate(() => ({
    clicks: window.__sortClicks43,
    commit: window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'),
  }));
  assert(armedOpacity43 === '0.8', `dragging the title past the 5px threshold arms the drag (header opacity got '${armedOpacity43}')`);
  assert(!!result43.commit, 'the drag commits a reorder on release');
  assert(result43.commit.args[2] === 'B', `commit targets column B (got '${result43.commit && result43.commit.args[2]}')`);
  assert(result43.clicks === 0, `the completed drag never also fires the sort button's click (got ${result43.clicks} clicks)`);

  // ---------------------------------------------------------------
  // TEST 44 — Header-drag redesign (c): Escape mid-drag still cancels and
  // glides back to identity (pre-existing behavior) even when the drag was
  // armed from the title instead of the grip — no commit, and the abandoned
  // drag's eventual (real) mouseup/click still never reaches the sort
  // button, because click-suppression stays armed until it actually
  // consumes one.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g44">
        <table id="tbl44" style="width:300px">
          <thead><tr>
            <th id="th44A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn44A" data-slot="datagrid-sort-button" style="width:100%;height:100%;">A</button></th>
            <th id="th44B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn44B" data-slot="datagrid-sort-button" style="width:100%;height:100%;">B</button></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
    window.__sortClicks44 = 0;
    document.getElementById('btn44A').addEventListener('click', () => { window.__sortClicks44++; });
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g44', window.__fakeDotNet);
  });
  const btn44A = await page.$('#btn44A');
  const box44A = await btn44A.boundingBox();
  await page.mouse.move(box44A.x + box44A.width / 2, box44A.y + box44A.height / 2);
  await page.mouse.down();
  await page.mouse.move(box44A.x + box44A.width / 2 + 20, box44A.y + box44A.height / 2, { steps: 3 });
  const armedOpacity44 = await page.evaluate(() => document.getElementById('th44A').style.opacity);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await page.mouse.up();
  await page.waitForTimeout(50);
  const result44 = await page.evaluate(() => ({
    clicks: window.__sortClicks44,
    commit: window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'),
    transform: document.getElementById('th44A').style.transform,
    opacity: document.getElementById('th44A').style.opacity,
  }));
  assert(armedOpacity44 === '0.8', 'the title-initiated drag armed before Escape');
  assert(!result44.commit, 'Escape mid-drag never commits a reorder');
  assert(result44.clicks === 0, `Escape mid-drag's eventual release never fires the sort button's click either (got ${result44.clicks})`);
  assert(result44.transform === '', `the header glides back to identity transform after Escape (got '${result44.transform}')`);
  assert(result44.opacity === '', `the header's lift opacity is restored after the Escape glide-back (got '${result44.opacity}')`);

  // ---------------------------------------------------------------
  // TEST 45 — Header-drag redesign (d): a drag attempt starting on a zone-C
  // action button (anything other than the sort button — a menu/pin/filter
  // trigger) never arms a reorder and never claims the grid's drag arbiter,
  // no matter how far the pointer moves afterwards.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g45">
        <table id="tbl45" style="width:300px">
          <thead><tr>
            <th id="th45A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span>
              <button id="btn45A" data-slot="datagrid-sort-button" style="width:60%;height:100%;">A</button>
              <button id="menu45A">Menu</button>
            </th>
            <th id="th45B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn45B" data-slot="datagrid-sort-button" style="width:100%;height:100%;">B</button></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g45', window.__fakeDotNet);
  });
  const menu45A = await page.$('#menu45A');
  const boxMenu45A = await menu45A.boundingBox();
  const th45B = await page.$('#th45B');
  const boxTh45B = await th45B.boundingBox();
  await page.mouse.move(boxMenu45A.x + boxMenu45A.width / 2, boxMenu45A.y + boxMenu45A.height / 2);
  await page.mouse.down();
  await page.mouse.move(boxTh45B.x + boxTh45B.width / 2, boxTh45B.y + boxTh45B.height / 2, { steps: 6 });
  const midOpacity45 = await page.evaluate(() => document.getElementById('th45A').style.opacity);
  await page.mouse.up();
  await page.waitForTimeout(50);
  const commit45 = await page.evaluate(() => window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'));
  assert(midOpacity45 === '', `dragging from a zone-C button never arms the reorder lift (header opacity got '${midOpacity45}')`);
  assert(!commit45, 'dragging from a zone-C button never commits a reorder');

  // ---------------------------------------------------------------
  // TEST 46 — Header-drag redesign (e): touch/pen long-press arming from the
  // title (zone B). (e1) a short tap (immediate release, no movement) never
  // arms and never preventDefaults the pointerdown, so a real device's
  // native tap-to-sort still reaches the button. (e2) a quick swipe (past
  // the cancel threshold BEFORE the long-press timer fires) is never
  // preventDefault'd either — native scroll wins — and waiting out the rest
  // of the long-press window afterwards must not retroactively arm it. (e3)
  // a real ~350ms press-and-hold with no significant movement arms the
  // drag, which can then be released over a sibling column to commit a
  // reorder exactly like the mouse/grip paths.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g46">
        <table id="tbl46" style="width:300px">
          <thead><tr>
            <th id="th46A" data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn46A" data-slot="datagrid-sort-button" style="width:100%;height:100%;">A</button></th>
            <th id="th46B" data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:150px">
              <span data-reorder-grip>::</span><button id="btn46B" data-slot="datagrid-sort-button" style="width:100%;height:100%;">B</button></th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g46', window.__fakeDotNet);
  });

  // (e1) short tap.
  const tap46 = await page.evaluate(() => {
    const btn = document.getElementById('btn46A');
    const rect = btn.getBoundingClientRect();
    const x = rect.x + rect.width / 2, y = rect.y + rect.height / 2;
    const down = new PointerEvent('pointerdown', { pointerId: 60, clientX: x, clientY: y, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' });
    const notPrevented = btn.dispatchEvent(down);
    btn.dispatchEvent(new PointerEvent('pointerup', { pointerId: 60, clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType: 'touch' }));
    return { notPrevented, opacity: document.getElementById('th46A').style.opacity };
  });
  assert(tap46.notPrevented === true, `a short touch tap on the title never preventDefaults the pointerdown (got ${tap46.notPrevented})`);
  assert(tap46.opacity === '', `a short touch tap never arms the drag lift (header opacity got '${tap46.opacity}')`);
  const commitAfterTap46 = await page.evaluate(() => window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'));
  assert(!commitAfterTap46, 'a short touch tap never commits a reorder');

  // (e2) quick swipe past the cancel threshold, well before the long-press
  // timer would fire.
  const swipe46 = await page.evaluate(() => {
    const btn = document.getElementById('btn46A');
    const rect = btn.getBoundingClientRect();
    const x = rect.x + rect.width / 2, y = rect.y + rect.height / 2;
    btn.dispatchEvent(new PointerEvent('pointerdown', { pointerId: 61, clientX: x, clientY: y, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' }));
    const move = new PointerEvent('pointermove', { pointerId: 61, clientX: x, clientY: y + 20, bubbles: true, cancelable: true, pointerType: 'touch' });
    const moveNotPrevented = btn.dispatchEvent(move);
    return { moveNotPrevented };
  });
  await page.waitForTimeout(450); // past the 350ms long-press window — must NOT retroactively arm
  const afterSwipeWait46 = await page.evaluate(() => document.getElementById('th46A').style.opacity);
  await page.evaluate(() => {
    document.getElementById('btn46A').dispatchEvent(new PointerEvent('pointerup', { pointerId: 61, bubbles: true, cancelable: true, pointerType: 'touch' }));
  });
  assert(swipe46.moveNotPrevented === true, `a quick swipe past the cancel threshold is never preventDefault'd — native scroll wins (got ${swipe46.moveNotPrevented})`);
  assert(afterSwipeWait46 === '', `a cancelled long-press never arms even after the long-press window elapses (header opacity got '${afterSwipeWait46}')`);

  // (e3) a real long-press: press-and-hold ~350ms with no significant
  // movement arms the drag; releasing over a sibling column then commits a
  // reorder.
  const btn46A = await page.$('#btn46A');
  const box46A = await btn46A.boundingBox();
  const th46B = await page.$('#th46B');
  const box46B = await th46B.boundingBox();
  await page.evaluate(([x, y]) => {
    document.getElementById('btn46A').dispatchEvent(new PointerEvent('pointerdown', { pointerId: 62, clientX: x, clientY: y, bubbles: true, cancelable: true, button: 0, pointerType: 'touch' }));
  }, [box46A.x + box46A.width / 2, box46A.y + box46A.height / 2]);
  await page.waitForTimeout(400); // past REORDER_LONGPRESS_MS (350ms)
  const armedOpacity46 = await page.evaluate(() => document.getElementById('th46A').style.opacity);
  await page.evaluate(([x, y]) => {
    document.getElementById('btn46A').dispatchEvent(new PointerEvent('pointermove', { pointerId: 62, clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType: 'touch' }));
  }, [box46B.x + box46B.width / 2, box46B.y + box46B.height / 2]);
  await page.evaluate(([x, y]) => {
    document.getElementById('btn46A').dispatchEvent(new PointerEvent('pointerup', { pointerId: 62, clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType: 'touch' }));
  }, [box46B.x + box46B.width / 2, box46B.y + box46B.height / 2]);
  await page.waitForTimeout(300);
  const commit46 = await page.evaluate(() => window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'));
  assert(armedOpacity46 === '0.8', `a ~350ms press-and-hold with no significant movement arms the drag (header opacity got '${armedOpacity46}')`);
  assert(!!commit46, 'the long-press-armed touch drag commits a reorder on release over a sibling column');
  assert(commit46.args[2] === 'B', `the long-press touch drag commits targeting column B (got '${commit46 && commit46.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 47 — Unified drag-to-group (rc.42), scenario (a): dragging a
  // Groupable column's grip onto the group panel commits via the
  // GROUP_PANEL_DROP_TARGET_ID sentinel — the SAME OnColumnReorderCommit
  // channel reorder itself uses, per DataGrid.razor's GroupPanelDropTargetId.
  // Also proves the live sibling-shift preview a mid-row pass engaged gets
  // relaxed back to identity the instant the pointer crosses into the panel,
  // and that the panel's data-drop-target highlight only lights up while the
  // drag is actually hovering it, clearing again once the drag ends.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g47">
        <div data-slot="datagrid-group-panel" style="height:40px;width:340px;">Drop to group</div>
        <table id="tbl47" style="width:340px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:110px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" data-groupable="true" style="width:110px">
              <span data-reorder-grip>::</span>B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:110px">
              <span data-reorder-grip>::</span>C</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td><td>c</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g47', window.__fakeDotNet);
  });
  const gripB47 = await page.$('div[data-grid-id="g47"] th[data-col-id="B"] [data-reorder-grip]');
  const gripB47Box = await gripB47.boundingBox();
  const thC47 = await page.$('div[data-grid-id="g47"] th[data-col-id="C"]');
  const cBox47 = await thC47.boundingBox();
  const panel47 = await page.$('div[data-grid-id="g47"] [data-slot="datagrid-group-panel"]');
  const panelBox47 = await panel47.boundingBox();

  await page.mouse.move(gripB47Box.x + gripB47Box.width / 2, gripB47Box.y + gripB47Box.height / 2);
  await page.mouse.down();
  // Pass over C first — engages the live sibling-shift reorder preview. Reads
  // the raw inline style (not getComputedStyle) throughout this test — the
  // shift is CSS-transitioned (~220ms), so a computed-style read moments after
  // the JS write would observe a mid-transition INTERPOLATED value instead of
  // the target applyProjection actually wrote (Codex-style false negative).
  await page.mouse.move(cBox47.x + cBox47.width / 2, cBox47.y + cBox47.height / 2, { steps: 5 });
  const cTxMidRow47 = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g47"] th[data-col-id="C"]').style.transform);
  assert(cTxMidRow47 !== '', `dragging B across C engages C's live sibling-shift preview first (got '${cTxMidRow47}')`);

  // Now move UP into the group panel.
  await page.mouse.move(panelBox47.x + panelBox47.width / 2, panelBox47.y + panelBox47.height / 2, { steps: 5 });
  const overPanel47 = await page.evaluate(() => {
    const panel = document.querySelector('div[data-grid-id="g47"] [data-slot="datagrid-group-panel"]');
    const c = document.querySelector('div[data-grid-id="g47"] th[data-col-id="C"]');
    return { panelDropTarget: panel.getAttribute('data-drop-target'), cTransform: c.style.transform };
  });
  assert(overPanel47.panelDropTarget === 'true', `panel gets data-drop-target="true" while a Groupable column hovers it (got '${overPanel47.panelDropTarget}')`);
  assert(overPanel47.cTransform === '', `entering panel mode relaxes C's live-shift preview back to identity (got '${overPanel47.cTransform}')`);

  await page.mouse.up();
  await page.waitForTimeout(300);
  const afterRelease47 = await page.evaluate(() => {
    const panel = document.querySelector('div[data-grid-id="g47"] [data-slot="datagrid-group-panel"]');
    return { panelDropTarget: panel.getAttribute('data-drop-target'), calls: window.__dotnetCalls };
  });
  const commit47 = afterRelease47.calls.find((c) => c.method === 'OnColumnReorderCommit');
  assert(afterRelease47.panelDropTarget === null, 'panel highlight is cleared once the drag ends (commit or cancel)');
  assert(!!commit47, 'releasing a Groupable column over the panel commits exactly once');
  assert(commit47.args[1] === 'B', `commit sources from the dragged column B (got '${commit47 && commit47.args[1]}')`);
  assert(commit47.args[2] === '__group-panel__', `commit targets the group-panel sentinel, not a real column id (got '${commit47 && commit47.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 48 — Unified drag-to-group (rc.42), scenario (b): the SAME drag,
  // but released back in the header row instead of the panel — must reorder,
  // NOT group. Proves mode-switching is fully reversible mid-drag, not a
  // one-way trip once the pointer has touched the panel.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g48">
        <div data-slot="datagrid-group-panel" style="height:40px;width:340px;">Drop to group</div>
        <table id="tbl48" style="width:340px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:110px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" data-groupable="true" style="width:110px">
              <span data-reorder-grip>::</span>B</th>
            <th data-col-id="C" data-col-pin="None" data-reorderable="true" style="width:110px">
              <span data-reorder-grip>::</span>C</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td><td>c</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g48', window.__fakeDotNet);
  });
  const gripB48 = await page.$('div[data-grid-id="g48"] th[data-col-id="B"] [data-reorder-grip]');
  const gripB48Box = await gripB48.boundingBox();
  const thC48 = await page.$('div[data-grid-id="g48"] th[data-col-id="C"]');
  const cBox48 = await thC48.boundingBox();
  const panel48 = await page.$('div[data-grid-id="g48"] [data-slot="datagrid-group-panel"]');
  const panelBox48 = await panel48.boundingBox();

  await page.mouse.move(gripB48Box.x + gripB48Box.width / 2, gripB48Box.y + gripB48Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(panelBox48.x + panelBox48.width / 2, panelBox48.y + panelBox48.height / 2, { steps: 5 });
  const overPanel48 = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g48"] [data-slot="datagrid-group-panel"]').getAttribute('data-drop-target'));
  assert(overPanel48 === 'true', 'panel highlights while hovering it mid-drag (setup for the return-to-row check)');
  // Move back down onto C, in the header row — leaves panel mode.
  await page.mouse.move(cBox48.x + cBox48.width / 2, cBox48.y + cBox48.height / 2, { steps: 5 });
  const backInRow48 = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g48"] [data-slot="datagrid-group-panel"]').getAttribute('data-drop-target'));
  assert(backInRow48 === null, 'panel highlight clears the instant the pointer leaves it, back over the row');

  await page.mouse.up();
  await page.waitForTimeout(300);
  const commit48 = await page.evaluate(() => window.__dotnetCalls.find((c) => c.method === 'OnColumnReorderCommit'));
  assert(!!commit48, 'releasing back in the row still commits — a normal reorder');
  assert(commit48.args[2] === 'C', `commit targets sibling column C, NOT the group-panel sentinel (got '${commit48 && commit48.args[2]}')`);

  // ---------------------------------------------------------------
  // TEST 49 — Unified drag-to-group (rc.42), scenario (c): Escape while
  // hovering the panel cancels everything — no group, no reorder, no commit
  // call of any kind (mirrors Escape's existing cancel semantics anywhere
  // else in the row).
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g49">
        <div data-slot="datagrid-group-panel" style="height:40px;width:230px;">Drop to group</div>
        <table id="tbl49" style="width:230px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" data-groupable="true" style="width:115px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:115px">
              <span data-reorder-grip>::</span>B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g49', window.__fakeDotNet);
  });
  const gripA49 = await page.$('div[data-grid-id="g49"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA49Box = await gripA49.boundingBox();
  const panel49 = await page.$('div[data-grid-id="g49"] [data-slot="datagrid-group-panel"]');
  const panelBox49 = await panel49.boundingBox();

  await page.mouse.move(gripA49Box.x + gripA49Box.width / 2, gripA49Box.y + gripA49Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(panelBox49.x + panelBox49.width / 2, panelBox49.y + panelBox49.height / 2, { steps: 5 });
  const overPanel49 = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g49"] [data-slot="datagrid-group-panel"]').getAttribute('data-drop-target'));
  assert(overPanel49 === 'true', 'panel highlights while hovering it (setup for the Escape-cancels check)');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  const afterEscape49 = await page.evaluate(() => {
    const panel = document.querySelector('div[data-grid-id="g49"] [data-slot="datagrid-group-panel"]');
    const a = document.querySelector('div[data-grid-id="g49"] th[data-col-id="A"]');
    return { panelDropTarget: panel.getAttribute('data-drop-target'), aOpacity: a.style.opacity, calls: window.__dotnetCalls };
  });
  await page.mouse.up(); // release whatever's left of the (already-cancelled) gesture
  assert(afterEscape49.panelDropTarget === null, 'Escape over the panel clears the highlight immediately');
  assert(afterEscape49.aOpacity === '', `Escape glides the dragged header back to identity, not left mid-drag (opacity '${afterEscape49.aOpacity}')`);
  assert(!afterEscape49.calls.some((c) => c.method === 'OnColumnReorderCommit'), 'Escape over the panel never fires a commit — no group, no reorder');

  // ---------------------------------------------------------------
  // TEST 50 — Unified drag-to-group (rc.42), scenario (d): a non-Groupable
  // column dragged onto the panel gets NO highlight, and releasing it there
  // is treated exactly like releasing outside any valid target — a plain
  // cancel, not a reorder and not a group.
  // ---------------------------------------------------------------
  await page.evaluate(() => {
    document.getElementById('host').innerHTML = `
      <div data-grid-id="g50">
        <div data-slot="datagrid-group-panel" style="height:40px;width:230px;">Drop to group</div>
        <table id="tbl50" style="width:230px">
          <thead><tr>
            <th data-col-id="A" data-col-pin="None" data-reorderable="true" style="width:115px">
              <span data-reorder-grip>::</span>A</th>
            <th data-col-id="B" data-col-pin="None" data-reorderable="true" style="width:115px">
              <span data-reorder-grip>::</span>B</th>
          </tr></thead>
          <tbody><tr><td>a</td><td>b</td></tr></tbody>
        </table>
      </div>`;
  });
  await page.evaluate(() => {
    window.__dotnetCalls.length = 0;
    window.__C.registerColumnReorder('g50', window.__fakeDotNet);
  });
  const gripA50 = await page.$('div[data-grid-id="g50"] th[data-col-id="A"] [data-reorder-grip]');
  const gripA50Box = await gripA50.boundingBox();
  const panel50 = await page.$('div[data-grid-id="g50"] [data-slot="datagrid-group-panel"]');
  const panelBox50 = await panel50.boundingBox();

  await page.mouse.move(gripA50Box.x + gripA50Box.width / 2, gripA50Box.y + gripA50Box.height / 2);
  await page.mouse.down();
  await page.mouse.move(panelBox50.x + panelBox50.width / 2, panelBox50.y + panelBox50.height / 2, { steps: 5 });
  const overPanel50 = await page.evaluate(() =>
    document.querySelector('div[data-grid-id="g50"] [data-slot="datagrid-group-panel"]').getAttribute('data-drop-target'));
  assert(overPanel50 === null, `non-Groupable column A never lights up the panel highlight (got '${overPanel50}')`);
  await page.mouse.up();
  await page.waitForTimeout(300);
  const afterRelease50 = await page.evaluate(() => window.__dotnetCalls);
  assert(!afterRelease50.some((c) => c.method === 'OnColumnReorderCommit'), 'dropping a non-Groupable column on the panel commits nothing — treated as a cancel');

  console.log(`\nALL TESTS PASSED (${passCount} assertions) — engine: ${ENGINE}`);
  await browser.close();
  server.close();
})().catch((e) => {
  console.error(`TEST SUITE FAILED on engine "${ENGINE}" (${passCount} assertions passed before the failure):`, e.message);
  process.exit(1);
});
