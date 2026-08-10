const charts = new Map();
let echartsLoaded = false;
let echartsLoadPromise = null;
let lumeoThemeRegistered = false;

// Auto-repaint all existing charts when the app theme changes.
// theme.js fires this event after setMode / setScheme / setRadius / setStyle /
// setBaseColor / setMenuColor / setMenuAccent / setFont / toggle — so any path
// the consumer takes (ThemeService, CustomizerSidebar, manual JS) repaints.
// Deferred with queueMicrotask so DOM class / CSS-var updates are already
// committed before we read them back in registerLumeoTheme().
if (typeof document !== 'undefined' && !window.__lumeoChartThemeListener) {
    document.addEventListener('lumeo:theme-changed', () => {
        if (charts.size === 0) return;
        queueMicrotask(() => {
            try { refreshAllCharts(); } catch (_) { /* ignore */ }
        });
    });
    window.__lumeoChartThemeListener = true;
}

// Same repaint path, driven by the OS/browser-level reduced-motion preference
// rather than an app theme swap. ECharts bakes animation flags into the option
// at setOption time — flipping the OS setting mid-session (or a browser devtools
// emulation toggle while the docs site is open) would otherwise leave already-
// rendered charts animating until their next unrelated re-render.
if (typeof window !== 'undefined' && window.matchMedia && !window.__lumeoChartMotionListener) {
    try {
        const mq = window.matchMedia('(prefers-reduced-motion: reduce)');
        const onChange = () => {
            if (charts.size === 0) return;
            queueMicrotask(() => {
                try { refreshAllCharts(); } catch (_) { /* ignore */ }
            });
        };
        if (typeof mq.addEventListener === 'function') mq.addEventListener('change', onChange);
        else if (typeof mq.addListener === 'function') mq.addListener(onChange); // Safari <14
        window.__lumeoChartMotionListener = true;
    } catch (_) { /* matchMedia unsupported — animations just use the initial read */ }
}

// True when the user (OS setting, or a browser's devtools emulation) has asked
// for reduced motion. Checked fresh on every theme (re)registration AND applied
// as a hard override in initChart/updateChart — see applyReducedMotion — so a
// consumer's own AnimationDuration/AnimationEasing parameter can never re-enable
// motion a11y setting has turned off.
function prefersReducedMotion() {
    try {
        return typeof window !== 'undefined' && !!window.matchMedia
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    } catch {
        return false;
    }
}

// Forces every ECharts animation off (entrance, update, emphasis/hover transitions —
// `animation:false` is the one switch that reaches all of them, see ECharts' own
// docs on the option) when reduced motion is active. Mutates and returns `options`
// so callers can use it inline. Split out as a pure function (no DOM/ECharts
// access) so it's covered by a plain computed-value unit test.
function applyReducedMotion(options, reducedMotion) {
    if (reducedMotion && options && typeof options === 'object') {
        options.animation = false;
    }
    return options;
}

function loadECharts(src) {
    if (echartsLoaded && window.echarts) return Promise.resolve();
    if (echartsLoadPromise) return echartsLoadPromise;

    echartsLoadPromise = new Promise((resolve, reject) => {
        if (window.echarts) {
            echartsLoaded = true;
            resolve();
            return;
        }
        const script = document.createElement('script');
        // Standard override: `window.lumeoCdn.echarts`; legacy `src` param takes
        // precedence so existing per-call overrides keep working.
        const globalOverride = (typeof window !== 'undefined' && window.lumeoCdn && window.lumeoCdn.echarts) || null;
        script.src = src || globalOverride || 'https://cdn.jsdelivr.net/npm/echarts@5/dist/echarts.min.js';
        script.onload = () => {
            echartsLoaded = true;
            resolve();
        };
        script.onerror = () => reject(new Error('Failed to load ECharts'));
        document.head.appendChild(script);
    });

    return echartsLoadPromise;
}

function resolveCssVars(obj) {
    if (!obj || typeof obj !== 'object') return;
    if (Array.isArray(obj)) {
        for (let i = 0; i < obj.length; i++) {
            if (typeof obj[i] === 'string') {
                if (obj[i].startsWith('var(')) {
                    obj[i] = resolveCssVarValue(obj[i]);
                } else if (isColorValue(obj[i])) {
                    obj[i] = colorToHex(obj[i]);
                }
            } else if (typeof obj[i] === 'object') {
                resolveCssVars(obj[i]);
            }
        }
    } else {
        for (const key of Object.keys(obj)) {
            if (typeof obj[key] === 'string') {
                if (obj[key].startsWith('var(')) {
                    obj[key] = resolveCssVarValue(obj[key]);
                } else if (isColorProperty(key) && isColorValue(obj[key])) {
                    obj[key] = colorToHex(obj[key]);
                }
            } else if (typeof obj[key] === 'object') {
                resolveCssVars(obj[key]);
            }
        }
    }
}

function isColorValue(str) {
    return str.startsWith('oklch(') || str.startsWith('hsl(') || str.startsWith('color(') || str.startsWith('lab(') || str.startsWith('lch(');
}

function isColorProperty(key) {
    const colorKeys = ['color', 'backgroundColor', 'borderColor', 'shadowColor', 'textBorderColor', 'textShadowColor'];
    return colorKeys.includes(key);
}

function resolveCssVarValue(str) {
    const match = str.match(/^var\(\s*(--[^,)]+)\s*(?:,\s*(.+))?\s*\)$/);
    if (!match) return str;
    const resolved = getCssVar(match[1]);
    return resolved || match[2] || str;
}

function getCssVar(name) {
    const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    if (!raw) return '';
    // Only convert color-like values to hex; leave non-color values (e.g. --radius) as-is
    if (raw.startsWith('#') || raw.startsWith('rgb') || raw.startsWith('hsl') ||
        raw.startsWith('oklch') || raw.startsWith('color(') || raw.startsWith('lab(') ||
        raw.startsWith('lch(') || raw.startsWith('hwb(')) {
        return colorToHex(raw);
    }
    return raw;
}

function colorToHex(color) {
    if (!color || color === 'transparent') return color;
    if (color.startsWith('#')) return color;

    // Step 1: Use DOM to resolve any CSS color (oklch, hsl, color(), etc.) to computed value
    const el = document.createElement('div');
    el.style.display = 'none';
    el.style.color = color;
    document.body.appendChild(el);
    const computed = getComputedStyle(el).color;
    document.body.removeChild(el);

    if (!computed) return color;

    // Step 2: Try to parse rgb/rgba (works in most cases)
    let m = computed.match(/rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)/);
    if (m) {
        return rgbToHex(Math.round(+m[1]), Math.round(+m[2]), Math.round(+m[3]));
    }

    // Step 3: Parse color(srgb r g b) — values are 0-1 floats
    m = computed.match(/color\(srgb\s+([\d.e+-]+)\s+([\d.e+-]+)\s+([\d.e+-]+)/);
    if (m) {
        return rgbToHex(
            Math.round(Math.max(0, Math.min(1, +m[1])) * 255),
            Math.round(Math.max(0, Math.min(1, +m[2])) * 255),
            Math.round(Math.max(0, Math.min(1, +m[3])) * 255)
        );
    }

    // Step 4: Parse color(display-p3 r g b) — approximate to sRGB
    m = computed.match(/color\(display-p3\s+([\d.e+-]+)\s+([\d.e+-]+)\s+([\d.e+-]+)/);
    if (m) {
        return rgbToHex(
            Math.round(Math.max(0, Math.min(1, +m[1])) * 255),
            Math.round(Math.max(0, Math.min(1, +m[2])) * 255),
            Math.round(Math.max(0, Math.min(1, +m[3])) * 255)
        );
    }

    // Step 5: Pixel-reading fallback — draw the computed color on canvas and read the pixel
    try {
        const canvas = document.createElement('canvas');
        canvas.width = 1;
        canvas.height = 1;
        const ctx = canvas.getContext('2d');
        // Try with the computed value first, then the original
        ctx.fillStyle = computed;
        ctx.fillRect(0, 0, 1, 1);
        const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
        // Check if canvas actually understood the color (not default black)
        if (r !== 0 || g !== 0 || b !== 0) {
            return rgbToHex(r, g, b);
        }
        // Try original color value
        ctx.clearRect(0, 0, 1, 1);
        ctx.fillStyle = color;
        ctx.fillRect(0, 0, 1, 1);
        const px = ctx.getImageData(0, 0, 1, 1).data;
        if (px[0] !== 0 || px[1] !== 0 || px[2] !== 0) {
            return rgbToHex(px[0], px[1], px[2]);
        }
    } catch {
        // Color parsing failed — unsupported format or CSS custom property; fall through to return original
    }

    return color;
}

function rgbToHex(r, g, b) {
    return '#' + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
}

/**
 * Builds the Lumeo ECharts theme object from a CSS-variable getter (`cssVar`,
 * normally {@link getCssVar}) and the current reduced-motion preference.
 *
 * Pulled out as a pure function — no `document`/`window.echarts` access — for two
 * reasons: (1) `echarts.registerTheme` only accepts a plain object, so this is the
 * natural seam; (2) it lets `__testing.buildLumeoTheme` be exercised with a fake
 * `cssVar` in a plain Node test (see tests/js/echarts-interop-theme.test.mjs),
 * asserting real computed values (numbers, booleans, exact colour strings) instead
 * of ever re-deriving them from a class name.
 *
 * Design decisions baked in here (rather than left for each of the 30 chart
 * wrappers to repeat) — see the charts-design PR description for the full
 * reasoning:
 *  - Legend is centred (`left: 'center'`) — reads as more deliberate than the
 *    ECharts default left-alignment, and applies to every wrapper's legend
 *    because none of them set `Left` themselves (theme values fill the gap left
 *    by an unset option property under ECharts' per-property option merge).
 *  - Value axes get a `minorTick`/`minorSplitLine` — genuinely finer tick
 *    density without any wrapper touching `splitNumber` (the property doesn't
 *    even exist on the typed `EChartAxis` model). Off by default in ECharts;
 *    turning it on here is the single place that changes it everywhere.
 *  - Tooltip mirrors the Lumeo `<PopoverContent>` surface (rounded-md, border,
 *    shadow-lg, `bg-popover`/`text-popover-foreground` equivalents) and fades via
 *    `transitionDuration` + an explicit `opacity`/`transform` CSS transition
 *    (ECharts' own tooltip DOM element honours real `var(--...)` — unlike the
 *    canvas-drawn series, no JS colour resolution is needed for it).
 *  - Hover: `emphasis.focus:'series'` + a matching `blur` state on every series
 *    type dims everything else instead of leaving other series at full opacity —
 *    "which series am I looking at" reads instantly on hover.
 *  - Entrance/update animation gets explicit duration/easing instead of relying
 *    on ECharts' own defaults, and reduced-motion forces `animation:false`
 *    (the one switch that reaches entrance, update, AND emphasis/hover
 *    transitions — see applyReducedMotion for the belt-and-braces option-level
 *    override applied on top of this).
 */
function buildLumeoTheme(cssVar, reducedMotion) {
    const fg = cssVar('--color-foreground') || '#1a1a1a';
    const mutedFg = cssVar('--color-muted-foreground') || '#737373';
    const border = cssVar('--color-border') || '#e5e5e5';
    const card = cssVar('--color-popover') || cssVar('--color-card') || '#ffffff';
    const popoverFg = cssVar('--color-popover-foreground') || fg;

    const chart1 = cssVar('--color-chart-1') || cssVar('--color-primary') || '#e85d04';
    const chart2 = cssVar('--color-chart-2') || '#2c9e8f';
    const chart3 = cssVar('--color-chart-3') || '#2d4f5c';
    const chart4 = cssVar('--color-chart-4') || '#d4a843';
    const chart5 = cssVar('--color-chart-5') || '#e08844';

    const radiusRaw = cssVar('--radius') || '0.75rem';
    const radiusPx = parseFloat(radiusRaw) * (radiusRaw.includes('rem') ? 16 : 1);
    const barRadius = [radiusPx, radiusPx, 0, 0];
    // Popover uses `rounded-md` (--radius-md == --radius by default) — tooltip
    // matches it exactly rather than re-deriving its own scale.
    const tooltipRadiusPx = radiusPx;

    const noStroke = { textBorderWidth: 0, textBorderColor: 'transparent', textShadowBlur: 0, textShadowColor: 'transparent' };
    const labelNoStroke = { ...noStroke, color: mutedFg, fontSize: 11 };

    // Entrance vs update get different rhythms — a longer, gentler entrance read
    // as "the chart is arriving"; a snappier update reads as "the data changed"
    // without redrawing the whole scene every time. cubicOut matches shadcn/
    // Radix's own default ease-out feel closely enough that it doesn't clash with
    // the rest of a Lumeo app's motion.
    const animation = reducedMotion
        ? { animation: false }
        : {
            animationDuration: 700,
            animationEasing: 'cubicOut',
            animationDurationUpdate: 400,
            animationEasingUpdate: 'cubicOut',
        };

    // Real fade: ECharts' own `transitionDuration` smooths tooltip repositioning,
    // but the show/hide (opacity 0<->1) edge reads as an instant snap without an
    // explicit CSS transition on the DOM node itself — extraCssText below adds
    // one. Reduced motion collapses both to ~0 so the tooltip still appears
    // instantly rather than "not fading in" (which would read as broken, not
    // as an a11y accommodation).
    const tooltipTransitionSeconds = reducedMotion ? 0 : 0.22;
    const tooltipTransitionMs = Math.round(tooltipTransitionSeconds * 1000);

    // Dim (not hide) every series except the hovered one — "which series am I
    // looking at" without the chart jumping around. Reused verbatim across every
    // series type below so the behaviour is one decision, not thirty.
    const blurOpacity = { opacity: 0.32 };
    const seriesHover = {
        emphasis: { focus: 'series' },
        blur: { itemStyle: blurOpacity, lineStyle: blurOpacity, areaStyle: blurOpacity, label: { opacity: 0.32 } },
    };

    return {
        color: [chart1, chart2, chart3, chart4, chart5],
        backgroundColor: 'transparent',
        ...animation,
        textStyle: {
            color: mutedFg,
            fontFamily: (typeof document !== 'undefined' && getComputedStyle(document.body).fontFamily) || 'system-ui, sans-serif',
            fontSize: 12,
            ...noStroke
        },
        title: {
            textStyle: { color: fg, fontWeight: 600, fontSize: 14 },
            subtextStyle: { color: mutedFg, fontSize: 12 }
        },
        legend: {
            // Centred reads as a deliberate design choice, not a default. Only
            // `left` is set — `top`/`bottom` are left for each wrapper to decide
            // (several anchor the legend to the bottom via `Bottom:"0%"`; setting
            // a theme-level `top` here would fight that and squash the legend).
            left: 'center',
            textStyle: { color: mutedFg, fontSize: 11, ...noStroke },
            icon: radiusPx > 0 ? 'roundRect' : 'rect',
            itemWidth: 12,
            itemHeight: 8,
            itemGap: 16,
            pageIconColor: mutedFg,
            pageIconInactiveColor: border,
            pageTextStyle: { color: mutedFg }
        },
        tooltip: {
            backgroundColor: card,
            borderColor: border,
            borderWidth: 1,
            padding: [8, 12],
            textStyle: { color: popoverFg, fontSize: 12 },
            // enterable:false + confine:true keep the tooltip from becoming a
            // second interactive surface and from drifting outside the chart
            // bounds on charts pinned near a viewport edge.
            enterable: false,
            confine: true,
            transitionDuration: tooltipTransitionSeconds,
            hideDelay: 60,
            showDelay: 0,
            axisPointer: {
                lineStyle: { color: border, width: 1 },
                crossStyle: { color: border, width: 1 },
                shadowStyle: { color: 'rgba(148,163,184,0.14)' },
                label: { backgroundColor: card, color: popoverFg, borderColor: border, borderWidth: 1 }
            },
            // The popover surface, reproduced 1:1: rounded-md, border, shadow-lg,
            // bg-popover/text-popover-foreground. box-shadow/border-radius reference
            // the live CSS variables directly (this is real tooltip DOM, not
            // canvas, so var(...) resolves natively without our JS colour
            // resolver) with the computed px value as a safety fallback.
            extraCssText: `border-radius: var(--radius-md, ${tooltipRadiusPx}px); box-shadow: var(--shadow-lg, 0 10px 15px -3px rgba(0,0,0,0.1), 0 4px 6px -4px rgba(0,0,0,0.1)); transition: opacity ${tooltipTransitionSeconds}s cubic-bezier(0.16,1,0.3,1), transform ${tooltipTransitionSeconds}s cubic-bezier(0.16,1,0.3,1); font-family: inherit;`
        },
        categoryAxis: {
            axisLine: { show: false },
            axisTick: { show: false },
            axisLabel: { color: mutedFg, fontSize: 11, ...noStroke },
            splitLine: { show: false }
        },
        valueAxis: {
            axisLine: { show: false },
            axisTick: { show: false },
            axisLabel: { color: mutedFg, fontSize: 11, ...noStroke },
            splitLine: {
                show: true,
                lineStyle: { color: border, type: 'dashed', opacity: 0.5 }
            },
            // Finer tick density without touching a single chart wrapper: a minor
            // tick + a faint minor gridline between each major split. Off by
            // default in ECharts; this is the one place that turns it on.
            minorTick: { show: true, splitNumber: 5 },
            minorSplitLine: { show: true, lineStyle: { color: border, opacity: 0.2 } }
        },
        label: labelNoStroke,
        line: {
            smooth: true,
            symbolSize: 0,
            lineStyle: { width: 2 },
            label: labelNoStroke,
            emphasis: { focus: 'series', lineStyle: { width: 3 } },
            blur: seriesHover.blur
        },
        bar: {
            barMaxWidth: 32,
            itemStyle: { borderRadius: barRadius },
            label: labelNoStroke,
            emphasis: { focus: 'series', itemStyle: { shadowBlur: 8, shadowColor: 'rgba(0,0,0,0.18)' } },
            blur: { itemStyle: blurOpacity, label: { opacity: 0.32 } }
        },
        pie: {
            itemStyle: { borderColor: card, borderWidth: 2 },
            label: labelNoStroke,
            emphasis: { scale: true, scaleSize: 6, itemStyle: { shadowBlur: 14, shadowColor: 'rgba(0,0,0,0.22)' } },
            blur: { itemStyle: blurOpacity, label: { opacity: 0.32 } }
        },
        radar: {
            axisName: { color: mutedFg, fontSize: 11 },
            splitLine: { lineStyle: { color: border, opacity: 0.4 } },
            splitArea: { areaStyle: { color: ['transparent', 'transparent'] } },
            axisLine: { lineStyle: { color: border, opacity: 0.3 } },
            label: labelNoStroke,
            emphasis: { focus: 'series', lineStyle: { width: 3 } },
            blur: seriesHover.blur
        },
        scatter: {
            symbolSize: 8,
            itemStyle: { opacity: 0.75 },
            label: labelNoStroke,
            emphasis: { focus: 'series', scale: 1.25, itemStyle: { opacity: 1, shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.2)' } },
            blur: { itemStyle: { opacity: 0.25 } }
        },
        graph: {
            lineStyle: { color: border, opacity: 0.6 },
            label: labelNoStroke,
            emphasis: { focus: 'adjacency', scale: true, lineStyle: { width: 3 } },
            blur: { itemStyle: blurOpacity, lineStyle: { opacity: 0.15 } }
        },
        sankey: {
            label: labelNoStroke,
            emphasis: { focus: 'adjacency' },
            blur: { itemStyle: blurOpacity, lineStyle: blurOpacity }
        },
        funnel: {
            label: labelNoStroke,
            emphasis: { focus: 'self', label: { fontWeight: 600 } },
            blur: { itemStyle: blurOpacity }
        },
        treemap: {
            label: labelNoStroke,
            breadcrumb: { itemStyle: { color: card, borderColor: border, textStyle: { color: mutedFg } } }
        },
        sunburst: {
            label: labelNoStroke,
            emphasis: { focus: 'ancestor' }
        },
        tree: {
            label: labelNoStroke,
            lineStyle: { color: border },
            emphasis: { focus: 'descendant' }
        },
        themeRiver: {
            label: labelNoStroke,
            emphasis: { focus: 'series' },
            blur: { itemStyle: blurOpacity }
        },
        heatmap: {
            label: labelNoStroke,
            emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.25)' } }
        },
        boxplot: {
            label: labelNoStroke,
            emphasis: { focus: 'series', itemStyle: { borderWidth: 2 } },
            blur: { itemStyle: blurOpacity }
        },
        candlestick: {
            label: labelNoStroke,
            emphasis: { focus: 'series' },
            blur: { itemStyle: blurOpacity }
        },
        parallel: {
            label: labelNoStroke,
            emphasis: { focus: 'series', lineStyle: { width: 3 } },
            blur: { lineStyle: { opacity: 0.12 } }
        },
        gauge: {
            axisLine: { lineStyle: { color: [[1, border]] } },
            axisTick: { show: false },
            splitLine: { show: false },
            axisLabel: { color: mutedFg, ...noStroke },
            detail: { color: fg, fontWeight: 600, ...noStroke },
            title: { color: mutedFg, ...noStroke }
        }
    };
}

function registerLumeoTheme() {
    if (lumeoThemeRegistered || !window.echarts) return;
    window.echarts.registerTheme('lumeo', buildLumeoTheme(getCssVar, prefersReducedMotion()));
    lumeoThemeRegistered = true;
}

export async function initChart(elementId, optionsJson, theme, echartsSource) {
    await loadECharts(echartsSource);

    const el = document.getElementById(elementId);
    if (!el) return;

    // Dispose existing instance if any
    if (charts.has(elementId)) {
        const prev = charts.get(elementId);
        if (prev._lumeoObserver) prev._lumeoObserver.disconnect();
        prev.dispose();
        charts.delete(elementId);
    }

    // Always re-register theme to pick up current CSS variable values (dark/light mode)
    lumeoThemeRegistered = false;
    registerLumeoTheme();
    const effectiveTheme = theme || 'lumeo';

    const chart = window.echarts.init(el, effectiveTheme, { renderer: 'canvas' });
    const options = JSON.parse(optionsJson);

    // Force remove text stroke/border from all series labels (ECharts adds white stroke by default)
    if (options.series) {
        for (const s of options.series) {
            if (s.label) {
                s.label.textBorderWidth = 0;
                s.label.textBorderColor = 'transparent';
                s.label.textShadowBlur = 0;
                s.label.textShadowColor = 'transparent';
            }
            if (s.emphasis?.label) {
                s.emphasis.label.textBorderWidth = 0;
                s.emphasis.label.textBorderColor = 'transparent';
                s.emphasis.label.textShadowBlur = 0;
                s.emphasis.label.textShadowColor = 'transparent';
            }
        }
    }

    // Resolve CSS var() references in options since ECharts renders on Canvas
    resolveCssVars(options);
    // Hard override: reduced motion wins even over a consumer's own
    // AnimationDuration/AnimationEasing parameter — see applyReducedMotion.
    applyReducedMotion(options, prefersReducedMotion());

    try {
        chart.setOption(options);
    } catch (e) {
        console.warn(`[Lumeo Chart] setOption failed for "${elementId}":`, e.message);
        // Retry once after a frame (helps wordcloud/extension race conditions)
        await new Promise(r => requestAnimationFrame(r));
        try { chart.setOption(options); } catch (e2) {
            console.error(`[Lumeo Chart] setOption retry failed for "${elementId}":`, e2.message);
        }
    }

    charts.set(elementId, chart);
    // Remember the PRISTINE (pre-resolveCssVars) option JSON and the theme name
    // this instance was created with — refreshAllCharts needs both to actually
    // re-resolve colours against the NEW live CSS variables. See the comment on
    // refreshAllCharts for why `chart.getOption()` cannot be used for this.
    chart._lumeoRawJson = optionsJson;
    chart._lumeoTheme = effectiveTheme;

    // Auto-resize on container resize
    const observer = new ResizeObserver(() => {
        chart.resize();
    });
    observer.observe(el);
    chart._lumeoObserver = observer;
}

export function updateChart(elementId, optionsJson, notMerge, replaceMergeJson) {
    const chart = charts.get(elementId);
    if (!chart) return;
    const options = JSON.parse(optionsJson);
    resolveCssVars(options);
    applyReducedMotion(options, prefersReducedMotion());
    const opts = { notMerge: notMerge || false };
    if (replaceMergeJson) {
        try {
            const replaceMerge = JSON.parse(replaceMergeJson);
            if (Array.isArray(replaceMerge) && replaceMerge.length > 0) {
                opts.replaceMerge = replaceMerge;
            }
        } catch { /* ignore bad input */ }
    }
    chart.setOption(options, opts);
    // Chart.razor always ships the FULL merged option here (never a partial
    // patch — see GetEffectiveJson/GetOptionsJson), so this stays the accurate
    // "pristine" snapshot for the next refreshAllCharts, same as initChart.
    chart._lumeoRawJson = optionsJson;
}

export function resizeChart(elementId) {
    const chart = charts.get(elementId);
    if (chart) chart.resize();
}

export function disposeChart(elementId) {
    const chart = charts.get(elementId);
    if (chart) {
        if (chart._lumeoObserver) {
            chart._lumeoObserver.disconnect();
        }
        chart.dispose();
        charts.delete(elementId);
    }
}

// Invalidate the cached Lumeo theme so the next registerLumeoTheme() re-reads
// CSS variables. Call this after the app swaps theme class (dark/light/palette)
// and before re-initializing a chart.
export function resetLumeoTheme() {
    lumeoThemeRegistered = false;
}

// Re-register theme and refresh all charts (call when theme changes).
//
// IMPORTANT: this must NOT rebuild `opts` from `chart.getOption()`. ECharts'
// getOption() always returns the fully MERGED/RESOLVED option — every themed
// default (axisLabel.color, tooltip.backgroundColor, legend.textStyle.color,
// the series `color` palette, ...) comes back baked in as concrete hex values,
// indistinguishable from something the consumer set explicitly. Re-feeding
// that into a freshly re-themed instance means the new theme's colours are
// immediately shadowed by the OLD theme's already-resolved ones — the exact
// "ECharts reads colours once at render time" trap the whole charts-design
// pass exists to fix. A theme/dark-mode switch would silently no-op on any
// already-rendered chart (confirmed via a live browser check: series colour
// stayed pinned to the light-mode hex after toggling dark + firing
// lumeo:theme-changed, even though --color-chart-1 itself had already updated).
//
// The fix: replay the PRISTINE option JSON each chart was last given (stashed
// on the instance by initChart/updateChart as `_lumeoRawJson`, BEFORE
// resolveCssVars ever touched it) through a fresh resolveCssVars pass against
// the NOW-current CSS variables, into a newly re-themed instance. Anything the
// chart doesn't set explicitly (which is most theming — most wrappers never
// set `Color`) falls through to the theme's fresh values, same as first
// render. Falls back to getOption() only for a chart that somehow has no
// stashed raw JSON (defensive — should not happen via the normal Chart.razor
// path).
export function refreshAllCharts() {
    lumeoThemeRegistered = false;
    registerLumeoTheme();
    for (const [id, chart] of charts) {
        const el = document.getElementById(id);
        if (!el) continue;
        let opts;
        if (chart._lumeoRawJson) {
            opts = JSON.parse(chart._lumeoRawJson);
            resolveCssVars(opts);
            applyReducedMotion(opts, prefersReducedMotion());
        } else {
            opts = chart.getOption();
        }
        const themeName = chart._lumeoTheme || 'lumeo';
        // Tear down the old ResizeObserver before disposing the chart. The
        // initChart path stores it on chart._lumeoObserver; without this
        // disconnect it kept observing the (still-mounted) element and
        // firing resize() calls into a disposed chart instance, leaving
        // both the observer and the dead instance attached to the DOM.
        if (chart._lumeoObserver) chart._lumeoObserver.disconnect();
        // Carry the registrations off the old instance before disposing it.
        const savedEvents = chart._lumeoEvents || [];
        const savedTooltip = chart._lumeoTooltip || null;
        chart.dispose();
        const newChart = window.echarts.init(el, themeName, { renderer: 'canvas' });
        newChart.setOption(opts);
        newChart._lumeoRawJson = chart._lumeoRawJson;
        newChart._lumeoTheme = themeName;
        // Re-attach the tooltip slot first (it calls setOption), then the event
        // handlers. Disposing the old chart dropped every chart.on(...) binding
        // and the custom tooltip.formatter, so OnClick/OnDataZoom/OnBrushSelected
        // and the <ChartTooltip> portal would otherwise go dead after a theme
        // change. Restore them onto the fresh instance.
        if (savedTooltip) {
            newChart._lumeoTooltip = savedTooltip;
            attachTooltipSlot(newChart, savedTooltip.portalId, savedTooltip.dotnetRef);
        }
        if (savedEvents.length) {
            newChart._lumeoEvents = savedEvents;
            for (const ev of savedEvents) attachChartEvent(newChart, ev.eventName, ev.dotnetRef);
        }
        // Re-attach a fresh observer so the new chart still auto-resizes;
        // disposeChart and the original initChart use the same pattern.
        const observer = new ResizeObserver(() => { newChart.resize(); });
        observer.observe(el);
        newChart._lumeoObserver = observer;
        charts.set(id, newChart);
    }
}

// Attach a single ECharts event handler to a live chart instance. Kept separate
// from registerChartEvent so refreshAllCharts() can re-attach the same handlers
// to the freshly re-initialized instance after a theme change.
function attachChartEvent(chart, eventName, dotnetRef) {
    chart.on(eventName, (params) => {
        const data = {
            name: params.name || '',
            seriesName: params.seriesName || '',
            seriesIndex: params.seriesIndex ?? -1,
            dataIndex: params.dataIndex ?? -1,
            value: params.value != null ? JSON.stringify(params.value) : '',
            componentType: params.componentType || '',
        };
        dotnetRef.invokeMethodAsync('OnChartEvent', eventName, JSON.stringify(data));
    });
}

export function registerChartEvent(elementId, eventName, dotnetRef) {
    const chart = charts.get(elementId);
    if (!chart) return;
    // Remember the registration so a theme refresh (dispose + re-init) can
    // re-attach it; without this OnClick/OnDataZoom/OnBrushSelected went dead
    // after every lumeo:theme-changed.
    (chart._lumeoEvents || (chart._lumeoEvents = [])).push({ eventName, dotnetRef });
    attachChartEvent(chart, eventName, dotnetRef);
}

/**
 * Wire ECharts' tooltip.formatter to a Razor-rendered hidden DOM portal.
 *
 * The portal (rendered by the <ChartTooltip> Razor component into a
 * `<div id="${portalId}" style="display:none">…</div>` outside the chart host) holds
 * the consumer's RenderFragment markup. On each tooltip invocation we:
 *   1. Serialize the formatter params and ship them to Razor via
 *      `OnTooltipPointChange` — Razor updates the slot's context and re-renders
 *      the portal's innerHTML.
 *   2. Return the portal's CURRENT innerHTML to ECharts synchronously.
 *
 * First hover on a fresh portal returns the initial render (ChartTooltipContext.Empty);
 * the next hover (and every one after) returns the Razor-updated markup. This matches
 * the "near-real-time" pattern Material Charts uses and avoids the latency cost of an
 * async Promise-based formatter on every mouse move.
 */
export function registerTooltipSlot(elementId, portalId, dotnetRef) {
    const chart = charts.get(elementId);
    if (!chart) return;
    // Remember the slot wiring so refreshAllCharts() can restore the custom
    // formatter after the theme-change dispose + re-init (otherwise the
    // <ChartTooltip> portal went dead after lumeo:theme-changed).
    chart._lumeoTooltip = { portalId, dotnetRef };
    attachTooltipSlot(chart, portalId, dotnetRef);
}

// Wires the tooltip formatter onto a live chart instance. See registerTooltipSlot
// for the full portal contract; split out so a theme refresh can re-attach it.
function attachTooltipSlot(chart, portalId, dotnetRef) {
    // Throttle hover-notifications to one per animation frame — moving the cursor
    // continuously across a busy chart can fire dozens of formatter callbacks per
    // second, but Razor only needs one per visible point change.
    let lastSig = null;
    let pendingFrame = null;
    // Outer-scoped pointer to the most recent formatter payload. Without this the rAF
    // callback closes over whichever `p` was first in the frame window — a rapid mouse
    // drag would then ship the stale first-point to Razor while the user's already on
    // a different point. Updating latestPoint on every formatter call keeps the rAF
    // send aligned with what the user is currently hovering.
    let latestPoints = null;

    chart.setOption({
        tooltip: {
            formatter: function (params) {
                // Axis-trigger fires with an ARRAY of points (one per series at the
                // hovered x); item-trigger fires with a single point object. Normalize
                // to an array so the Razor slot can iterate across all series — the
                // previous code shipped only points[0] and silently hid every series
                // past the first.
                const arr = Array.isArray(params) ? params : (params ? [params] : []);
                if (arr.length === 0) return '';
                const head = arr[0];

                const sig = `${head.seriesIndex}|${head.dataIndex}|${head.name}|${arr.length}`;
                if (sig !== lastSig) {
                    lastSig = sig;
                    latestPoints = arr;
                    if (pendingFrame === null) {
                        pendingFrame = requestAnimationFrame(() => {
                            pendingFrame = null;
                            const current = latestPoints;
                            if (!current || current.length === 0) return;
                            try {
                                const projectPoint = (q) => ({
                                    seriesName: q.seriesName || '',
                                    seriesType: q.seriesType || '',
                                    seriesIndex: q.seriesIndex ?? 0,
                                    dataIndex: q.dataIndex ?? 0,
                                    value: q.value,
                                    color: typeof q.color === 'string' ? q.color : null,
                                    marker: typeof q.marker === 'string' ? q.marker : null,
                                });

                                const h = current[0];
                                // Forward a richer payload than the bare typed fields:
                                // ChartTooltipContext.Raw exposes everything here so
                                // consumers can pull formatter-only fields like
                                // percent / axisValueLabel / data without a second
                                // round-trip. points[] is the new entry that lets the
                                // Razor slot iterate across all series at the same x.
                                const payload = {
                                    seriesName: h.seriesName || '',
                                    seriesType: h.seriesType || '',
                                    seriesIndex: h.seriesIndex ?? 0,
                                    componentType: h.componentType || '',
                                    componentSubType: h.componentSubType || '',
                                    name: h.name || '',
                                    dataIndex: h.dataIndex ?? 0,
                                    value: h.value,
                                    data: h.data,
                                    color: typeof h.color === 'string' ? h.color : null,
                                    marker: typeof h.marker === 'string' ? h.marker : null,
                                    percent: h.percent ?? null,
                                    axisValue: h.axisValue ?? null,
                                    axisValueLabel: h.axisValueLabel ?? null,
                                    axisIndex: h.axisIndex ?? null,
                                    axisType: h.axisType ?? null,
                                    axisId: h.axisId ?? null,
                                    axisDim: h.axisDim ?? null,
                                    dimensionNames: h.dimensionNames ?? null,
                                    encode: h.encode ?? null,
                                    dataType: h.dataType ?? null,
                                    points: current.map(projectPoint),
                                };
                                dotnetRef.invokeMethodAsync('OnTooltipPointChange', JSON.stringify(payload));
                            } catch (_) {
                                // dotnetRef may have been disposed mid-hover — swallow.
                            }
                        });
                    }
                }

                const portal = document.getElementById(portalId);
                return portal ? portal.innerHTML : '';
            },
        },
    });
}

export function showLoading(elementId, opts) {
    const chart = charts.get(elementId);
    if (chart) chart.showLoading('default', opts || { text: '', maskColor: 'rgba(255,255,255,0.7)', spinnerRadius: 14, lineWidth: 2 });
}

export function hideLoading(elementId) {
    const chart = charts.get(elementId);
    if (chart) chart.hideLoading();
}

export function getDataURL(elementId, opts) {
    const chart = charts.get(elementId);
    if (!chart) return null;
    return chart.getDataURL(opts || { type: 'png', pixelRatio: 2, backgroundColor: '#fff' });
}

export function connectCharts(groupId, elementIds) {
    if (!window.echarts) return;
    const instances = elementIds.map(id => charts.get(id)).filter(Boolean);
    instances.forEach(c => c.group = groupId);
    window.echarts.connect(groupId);
}

export function disconnectCharts(groupId) {
    if (!window.echarts) return;
    window.echarts.disconnect(groupId);
}

// Set (or clear) a SINGLE chart's group membership. connectCharts only ever
// ASSIGNS a group, so a chart that switches/clears its Group parameter would stay
// wired to the old group (its cursor/zoom kept syncing with the old siblings).
// Passing a falsy groupId detaches the chart by setting chart.group = null so it no
// longer participates in echarts.connect() syncing; a non-empty groupId re-homes it.
export function setChartGroup(elementId, groupId) {
    const chart = charts.get(elementId);
    if (!chart) return;
    chart.group = groupId || null;
    if (window.echarts && groupId) window.echarts.connect(groupId);
}

export function appendData(elementId, seriesIndex, newData) {
    const chart = charts.get(elementId);
    if (!chart) return;
    const opt = chart.getOption();
    if (opt.series && opt.series[seriesIndex]) {
        const existingData = opt.series[seriesIndex].data || [];
        const parsed = typeof newData === 'string' ? JSON.parse(newData) : newData;
        opt.series[seriesIndex].data = [...existingData, ...parsed];
        chart.setOption(opt);
    }
}

export function dispatchAction(elementId, actionJson) {
    const chart = charts.get(elementId);
    if (!chart) return;
    const action = typeof actionJson === 'string' ? JSON.parse(actionJson) : actionJson;
    chart.dispatchAction(action);
}

export async function loadExtension(url, overrideKey, echartsSource) {
    // A host can redirect a plugin to a self-hosted copy (GDPR: no pre-consent CDN
    // hit) by setting window.lumeoCdn[overrideKey] — same mechanism loadECharts()
    // uses for `echarts`. Falls back to the caller's default (CDN) URL otherwise.
    const override = (overrideKey && typeof window !== 'undefined'
        && window.lumeoCdn && window.lumeoCdn[overrideKey]) || null;
    const resolved = override || url;
    if (!resolved) return;
    if (document.querySelector(`script[src="${resolved}"]`)) return;
    // Ensure ECharts core is loaded first — and honour the calling chart's own
    // `EChartsSource`. A plugin chart (word cloud / liquid fill) loads its extension
    // BEFORE its inner <Chart> mounts, so THIS is the first core load; without
    // forwarding the source it would fall back to jsDelivr even when the consumer
    // set a per-chart EChartsSource, defeating self-hosting. The extension script
    // itself still resolves via overrideKey/global keys (documented semantics).
    await loadECharts(echartsSource);
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = resolved;
        script.onload = resolve;
        script.onerror = () => reject(new Error(`Failed to load extension: ${resolved}`));
        document.head.appendChild(script);
    });
}

export async function registerMap(mapName, geoJson) {
    await loadECharts();
    if (!window.echarts) return;
    const json = typeof geoJson === 'string' ? JSON.parse(geoJson) : geoJson;
    window.echarts.registerMap(mapName, json);
}

// Test-only seam (mirrors scheduler.js's `__testing` export). None of these
// functions touch `document`/`window.echarts`, so they can be exercised with a
// plain Node test asserting real computed values — see
// tests/js/echarts-interop-theme.test.mjs. Not part of the public interop
// surface; Blazor's JS interop only ever calls the named exports above.
export const __testing = { buildLumeoTheme, prefersReducedMotion, applyReducedMotion };
