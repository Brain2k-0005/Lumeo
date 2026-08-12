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

test('type scale: axis/legend/data-label/radar text sizes are 12 (--text-xs), not the off-scale 11 a previous pass hardcoded', async () => {
    // Lumeo's type scale is 12 (text-xs) / 14 (text-sm) / ... — there is no 11.
    // charts-design-2 audit: every one of these hardcoded fontSize:11.
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.categoryAxis.axisLabel.fontSize, 12);
    assert.equal(theme.valueAxis.axisLabel.fontSize, 12);
    assert.equal(theme.legend.textStyle.fontSize, 12);
    assert.equal(theme.label.fontSize, 12); // labelNoStroke, shared by bar/pie/line/etc. data labels
    assert.equal(theme.radar.axisName.fontSize, 12);
    // Unchanged — already on-scale: top-level textStyle (12/text-xs), title (14/text-sm).
    assert.equal(theme.textStyle.fontSize, 12);
    assert.equal(theme.title.textStyle.fontSize, 14);
});

test('legend is centred — the one shared decision that reaches all 22 legend-using wrappers via theme merge', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.legend.left, 'center');
    // Deliberately NOT setting top/bottom — several wrappers (Pie/Donut/Funnel/
    // Nightingale) anchor the legend with Bottom:"0%"; a theme-level `top` would
    // fight that and squash the legend into a sliver.
    assert.equal(theme.legend.top, undefined);
});

test('value axis has ONE gridline system — no minor tick / minor split line chrome', async () => {
    // charts-design-2: a previous pass added a minor tick + finer minor gridline
    // between each major split, "genuinely finer tick density". Removed — nothing
    // else in Lumeo (Table, Card) layers a second, fainter grid of sub-lines behind
    // its primary divider, and the EvilCharts-aligned reference this pass matches
    // shows exactly one faint dashed gridline system, nothing finer underneath it.
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal('minorTick' in theme.valueAxis, false);
    assert.equal('minorSplitLine' in theme.valueAxis, false);
    assert.equal(theme.valueAxis.splitLine.show, true);
    assert.equal(theme.valueAxis.splitLine.lineStyle.type, 'dashed');
});

test('tooltip surface mirrors the Lumeo popover token-for-token, not a re-derived colour', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.tooltip.backgroundColor, '#f9f9f9'); // --color-popover, not --color-card
    assert.equal(theme.tooltip.textStyle.color, '#0a0a0a'); // --color-popover-foreground
    // border-border/60 — PopoverContent's exact border (not full-opacity).
    assert.equal(theme.tooltip.borderColor, 'rgba(221, 221, 221, 0.6)');
    assert.equal(theme.tooltip.borderWidth, 1);
    // padding: PopoverContent uses p-4 (16px, uniform), not ECharts' [8,12] default.
    assert.equal(theme.tooltip.padding, 16);
    // type: 14 (--text-sm) — matching Lumeo's own popover-family body-copy size
    // (DropdownMenuItem/SelectItem are both text-sm), not the smaller --text-xs.
    assert.equal(theme.tooltip.textStyle.fontSize, 14);
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

// --- charts-design "EvilCharts level" pass: gradients, glows, reveals ------
// The bar/pie gradients and the shadow-based glows are all DERIVED at build
// time (glow) or render time (gradient, via a callback) from an already-
// resolved hex colour — never a hardcoded hex. These tests assert the exact
// computed output of the pure helpers and of buildLumeoTheme's callbacks, not
// just that a key is present.

test('colour helpers: hexToRgb/withAlpha/lighten produce exact computed values, and fail safe on unparseable input', async () => {
    const { __testing } = await importInterop();
    assert.deepEqual(__testing.hexToRgb('#aa0000'), { r: 170, g: 0, b: 0 });
    assert.deepEqual(__testing.hexToRgb('#f00'), { r: 255, g: 0, b: 0 }); // 3-digit shorthand
    assert.equal(__testing.hexToRgb('rgba(1,2,3,0.5)'), null); // not a hex string — fail safe
    assert.equal(__testing.hexToRgb('not-a-color'), null);

    assert.equal(__testing.withAlpha('#aa0000', 0.16), 'rgba(170, 0, 0, 0.16)');
    // Unparseable input passes through unchanged rather than producing "rgba(NaN,...)"
    assert.equal(__testing.withAlpha('rgba(1,2,3,0.5)', 0.16), 'rgba(1,2,3,0.5)');

    // lighten mixes toward white by the given amount — exact rounded RGB math,
    // not "some other colour came out".
    assert.equal(__testing.lighten('#aa0000', 0.32), 'rgb(197, 82, 82)');
    assert.equal(__testing.lighten('#000000', 1), 'rgb(255, 255, 255)');
    assert.equal(__testing.lighten('#000000', 0), 'rgb(0, 0, 0)');
});

test('glow: brand-accent (--color-primary) tinted, not per-series — line gets a rest glow that intensifies on emphasis', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    // --color-primary in the fixture is #ff00ff, deliberately different from
    // every --color-chart-N so a regression that tints the glow off some
    // OTHER token is caught immediately.
    assert.equal(theme.line.lineStyle.shadowBlur, 6);
    assert.equal(theme.line.lineStyle.shadowColor, 'rgba(255, 0, 255, 0.16)');
    assert.equal(theme.line.emphasis.lineStyle.shadowBlur, 14);
    assert.equal(theme.line.emphasis.lineStyle.shadowColor, 'rgba(255, 0, 255, 0.34)');
    // Emphasis strictly stronger than rest — "subtle at rest, stronger on
    // emphasis", not a constant halo.
    assert.ok(theme.line.emphasis.lineStyle.shadowBlur > theme.line.lineStyle.shadowBlur);

    assert.equal(theme.bar.emphasis.itemStyle.shadowColor, 'rgba(255, 0, 255, 0.4)');
    assert.equal(theme.pie.emphasis.itemStyle.shadowColor, 'rgba(255, 0, 255, 0.45)');
    assert.equal(theme.scatter.itemStyle.shadowColor, 'rgba(255, 0, 255, 0.18)'); // scatter DOES glow at rest
    assert.equal(theme.scatter.emphasis.itemStyle.shadowColor, 'rgba(255, 0, 255, 0.4)');
    assert.equal(theme.heatmap.emphasis.itemStyle.shadowColor, 'rgba(255, 0, 255, 0.5)');
});

test('charts-design form pass: line emphasis enlarges the hovered marker (scale), not just the stroke', async () => {
    // "thicken the stroke or enlarge the marker" from the design-pass brief — both,
    // not colour alone, carry the hover state for line series.
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.line.emphasis.scale, 1.5);
    // Strictly larger than the resting (unscaled) marker.
    assert.ok(theme.line.emphasis.scale > 1);
});

test('bar: no gradient — itemStyle has no colour callback, only the token-driven borderRadius', async () => {
    // charts-design-2: removed the top-lightened vertical gradient a previous pass
    // built via an itemStyle.color callback. Nothing else in Lumeo (Button, Badge,
    // Card) fills a solid shape with a gradient, and the EvilCharts-aligned
    // reference this pass matches is explicit: "solid, no visible gradient". Bars
    // now render with ECharts' own resolved palette colour, untouched.
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal('color' in theme.bar.itemStyle, false);
    // radius: --radius is 0.5rem == 8px in this fixture; bar corners use HALF of
    // that (--radius-sm's own ratio: calc(var(--radius) * 0.5)) -> 4px, not the
    // full 8px. Geometry-gap pass: the full --radius token (tuned for cards/
    // buttons) rendered as an oversized pill cap on a bar column — confirmed
    // live at the library's own 12px-radius-on-a-13px-wide-bar ratio, where it
    // rounded into a near-full semicircle. Computed by halving the already-
    // resolved --radius here (not by re-reading --radius-sm) because
    // getComputedStyle().getPropertyValue() on a calc()-based custom property
    // returns its unresolved specified value, not a usable px number — see
    // buildLumeoTheme's barRadiusPx comment and resolveLengthPxViaProbe.
    assert.deepEqual(theme.bar.itemStyle.borderRadius, [4, 4, 0, 0]);
});

test('bar: geometry-gap pass — tighter barCategoryGap/barGap and a raised barMaxWidth, shared with every bar-family chart type', async () => {
    // Before this pass: ECharts' own defaults (barCategoryGap: '20%', barGap: '30%',
    // barMaxWidth: 32) measured out to ~13px bars in the library's own 12-category/
    // 2-series demo (480px chart width) — "narrow strips with large gaps". These are
    // theme-level (not bar-only C#), so PictorialBarChart/PolarBarChart/WaterfallChart/
    // MixedChart's bar series all inherit the same chunkier ratio through theme merge.
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(theme.bar.barCategoryGap, '10%');
    assert.equal(theme.bar.barGap, '8%');
    assert.equal(theme.bar.barMaxWidth, 40);
});

test('pie gradient: itemStyle.color is a callback that builds a centre-lightened RADIAL gradient from params.color', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);
    assert.equal(typeof theme.pie.itemStyle.color, 'function');

    const gradient = theme.pie.itemStyle.color({ color: '#00bb00' });
    assert.equal(gradient.type, 'radial');
    assert.deepEqual({ x: gradient.x, y: gradient.y, r: gradient.r }, { x: 0.5, y: 0.5, r: 0.7 });
    assert.equal(gradient.colorStops[0].color, __testing.lighten('#00bb00', 0.28));
    assert.equal(gradient.colorStops[1].color, '#00bb00');
});

test('reveal: bar/pie/scatter get a capped, staggered animationDelay; line relies on its native draw-in instead', async () => {
    const { __testing } = await importInterop();
    const theme = __testing.buildLumeoTheme(fakeCssVar, false);

    assert.equal(typeof theme.bar.animationDelay, 'function');
    assert.equal(theme.bar.animationDelay(0), 0);
    assert.equal(theme.bar.animationDelay(5), 225); // 5 * 45, under the cap
    assert.equal(theme.bar.animationDelay(100), 700); // capped, not 4500

    assert.equal(typeof theme.pie.animationDelay, 'function');
    assert.equal(theme.pie.animationDelay(2), 180); // 2 * 90
    assert.equal(theme.pie.animationDelay(50), 500); // capped

    assert.equal(typeof theme.scatter.animationDelay, 'function');
    assert.equal(theme.scatter.animationDelay(3), 30); // 3 * 10
    assert.equal(theme.scatter.animationDelay(1000), 400); // capped

    // Line/area has no per-point animationDelay (the shape is one continuous
    // path, not per-datum elements) — it instead gets a distinct, longer
    // entrance duration/easing than the shared 700ms default.
    assert.equal(theme.bar.animationDelay === theme.line.animationDelay, false);
    assert.equal('animationDelay' in theme.line, false);
    assert.equal(theme.line.animationDuration, 900);
    assert.equal(theme.line.animationEasing, 'cubicOut');
    assert.notEqual(theme.line.animationDuration, theme.animationDuration); // distinct from the shared 700ms default
});

test('reduced motion: reveal animationDelay functions and the line-specific duration are OMITTED, not just inert', async () => {
    const { __testing } = await importInterop();
    // Predicted WRONG value first: if this regressed to always adding the
    // delay/duration regardless of reducedMotion, these keys would be
    // present here too — confirm they are genuinely absent, not merely
    // harmless under the top-level animation:false override.
    const theme = __testing.buildLumeoTheme(fakeCssVar, true);
    assert.equal('animationDelay' in theme.bar, false);
    assert.equal('animationDelay' in theme.pie, false);
    assert.equal('animationDelay' in theme.scatter, false);
    assert.equal('animationDuration' in theme.line, false);
    assert.equal('animationEasing' in theme.line, false);
    // Presentational colour (the line's rest glow) is not motion — it stays
    // present under reduced motion (a static glow isn't animation and
    // prefers-reduced-motion has no opinion on it). Bar no longer has a
    // colour callback at all (gradient removed — see the "no gradient" test).
    assert.equal(theme.line.lineStyle.shadowBlur, 6);
});
