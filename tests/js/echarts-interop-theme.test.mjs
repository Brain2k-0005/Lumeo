// Regression tests for the "make Lumeo's ECharts charts beautiful" design pass
// (feat/charts-design). buildLumeoTheme / applyReducedMotion are pure functions
// with no document/window.echarts dependency (unlike registerLumeoTheme, which
// wraps them for the real DOM), so they can be exercised directly here with a
// fake CSS-variable getter — asserting actual computed theme values (numbers,
// booleans, exact strings), never class names or regexes against the bundle.
//
// Run with: node --test tests/js/echarts-interop-theme.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const interopJsPath = path.resolve(here, '../../src/Lumeo.Charts/wwwroot/js/echarts-interop.js');

// buildLumeoTheme calls getComputedStyle(document.body) for the font-family
// fallback, guarded by `typeof document !== 'undefined'`. We don't set
// globalThis.document, so that branch is skipped and the ECharts default
// 'system-ui, sans-serif' string is used instead — fine, font-family isn't
// under test here.
async function importInterop() {
    const url = pathToFileURL(interopJsPath).href;
    return import(url);
}

// A representative set of CSS variable values distinct enough that a test
// asserting the WRONG value (e.g. reading --color-primary instead of
// --color-chart-2) cannot coincidentally pass.
const FAKE_VARS = {
    '--color-foreground': '#111111',
    '--color-muted-foreground': '#666666',
    '--color-border': '#dddddd',
    '--color-popover': '#f9f9f9',
    '--color-popover-foreground': '#0a0a0a',
    '--color-card': '#fafafa',
    '--color-chart-1': '#aa0000',
    '--color-chart-2': '#00bb00',
    '--color-chart-3': '#0000cc',
    '--color-chart-4': '#dddd00',
    '--color-chart-5': '#00dddd',
    '--color-primary': '#ff00ff', // deliberately different from chart-1 for this test
    '--radius': '0.5rem', // 8px
};
const fakeCssVar = (name) => FAKE_VARS[name] || '';

test('legend is centred — the one shared decision that reaches all 22 legend-using wrappers via theme merge', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.legend.left, 'center');
    // Deliberately NOT setting top/bottom — several wrappers (Pie/Donut/Funnel/
    // Nightingale) anchor the legend with Bottom:"0%"; a theme-level `top` would
    // fight that and squash the legend into a sliver.
    assert.equal(theme.legend.top, undefined);
});

test('value axis gets a minor tick + minor split line for finer density, off by default in ECharts', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.valueAxis.minorTick.show, true);
    assert.equal(theme.valueAxis.minorTick.splitNumber, 5);
    assert.equal(theme.valueAxis.minorSplitLine.show, true);
});

test('tooltip surface mirrors the Lumeo popover token-for-token, not a re-derived colour', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.tooltip.backgroundColor, '#f9f9f9'); // --color-popover, not --color-card
    assert.equal(theme.tooltip.textStyle.color, '#0a0a0a'); // --color-popover-foreground
    assert.equal(theme.tooltip.borderColor, '#dddddd');
    assert.equal(theme.tooltip.borderWidth, 1);
    // radius: --radius is 0.5rem == 8px in this fixture.
    assert.match(theme.tooltip.extraCssText, /border-radius: var\(--radius-md, 8px\)/);
    assert.match(theme.tooltip.extraCssText, /box-shadow: var\(--shadow-lg,/);
});

test('tooltip fades — real transitionDuration + a CSS opacity/transform transition, not a snap', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.tooltip.transitionDuration, 0.22);
    assert.match(theme.tooltip.extraCssText, /transition: opacity 0\.22s/);
    assert.match(theme.tooltip.extraCssText, /transform 0\.22s/);
    assert.equal(theme.tooltip.enterable, false);
    assert.equal(theme.tooltip.confine, true);
});

test('reduced motion collapses the tooltip fade to ~0s instead of leaving it animated', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, true);
    assert.equal(theme.tooltip.transitionDuration, 0);
    assert.match(theme.tooltip.extraCssText, /transition: opacity 0s/);
});

test('entrance/update animation gets explicit, distinct durations', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.animationDuration, 700);
    assert.equal(theme.animationEasing, 'cubicOut');
    assert.equal(theme.animationDurationUpdate, 400);
    assert.equal(theme.animationEasingUpdate, 'cubicOut');
    assert.notEqual(theme.animationDuration, theme.animationDurationUpdate);
});

test('reduced motion sets the single animation:false switch instead of zeroing durations piecemeal', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, true);
    assert.equal(theme.animation, false);
    // The duration/easing keys must not be present at all — animation:false is
    // the documented ECharts switch; leaving stale durations around would be
    // misleading even though they'd be inert.
    assert.equal(theme.animationDuration, undefined);
});

test('hover: series types get an emphasis.focus + a matching blur — not left at full opacity', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);

    assert.equal(theme.line.emphasis.focus, 'series');
    assert.equal(theme.line.blur.lineStyle.opacity, 0.32);

    assert.equal(theme.bar.emphasis.focus, 'series');
    assert.equal(theme.bar.blur.itemStyle.opacity, 0.32);

    assert.equal(theme.pie.emphasis.scale, true);
    assert.equal(theme.pie.emphasis.scaleSize, 6);
    assert.equal(theme.pie.blur.itemStyle.opacity, 0.32);

    assert.equal(theme.scatter.emphasis.scale, 1.25);
});

test('chart-1 does not silently fall back to --color-primary when chart-1 is actually set (contrast trap regression)', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    // Fixture deliberately sets chart-1 and primary to DIFFERENT colours. If the
    // theme ever regresses to preferring --color-primary over an explicitly-set
    // --color-chart-1, this catches it immediately (this is the exact class of
    // bug called out in the task brief: chart-1 silently resolving to primary).
    assert.equal(theme.color[0], '#aa0000'); // --color-chart-1
    assert.notEqual(theme.color[0], fakeCssVar('--color-primary'));
});

test('applyReducedMotion sets options.animation=false only when reduced motion is active, and is a hard override', async () => {
    const { __testing } = await importInterop();

    const untouched = __testing.applyReducedMotion({ series: [] }, false);
    assert.equal(untouched.animation, undefined);

    // Simulates a consumer wrapper that explicitly requested a long animation via
    // AnimationDuration — reduced motion must win regardless.
    const overridden = __testing.applyReducedMotion({ animationDuration: 5000, animationEasing: 'elasticOut' }, true);
    assert.equal(overridden.animation, false);
    // The stale duration/easing are harmless once animation:false is set, but we
    // don't scrub them — applyReducedMotion's contract is the single override
    // flag, not a deep option sanitizer.
    assert.equal(overridden.animationDuration, 5000);
});

test('prefersReducedMotion is false in this Node environment (no window) — sanity check for the export shape', async () => {
    const { __testing } = await importInterop();
    assert.equal(__testing.prefersReducedMotion(), false);
});
