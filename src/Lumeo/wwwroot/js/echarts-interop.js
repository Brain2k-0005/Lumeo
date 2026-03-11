const charts = new Map();
let echartsLoaded = false;
let echartsLoadPromise = null;
let lumeoThemeRegistered = false;

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
        script.src = src || 'https://cdn.jsdelivr.net/npm/echarts@5/dist/echarts.min.js';
        script.onload = () => {
            echartsLoaded = true;
            resolve();
        };
        script.onerror = () => reject(new Error('Failed to load ECharts'));
        document.head.appendChild(script);
    });

    return echartsLoadPromise;
}

function getCssVar(name) {
    const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    if (!raw) return '';
    // Convert oklch/hsl/etc to hex so ECharts can use it
    try {
        const el = document.createElement('div');
        el.style.color = raw;
        document.body.appendChild(el);
        const computed = getComputedStyle(el).color;
        document.body.removeChild(el);
        if (computed) {
            const m = computed.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
            if (m) return '#' + ((1 << 24) + (+m[1] << 16) + (+m[2] << 8) + +m[3]).toString(16).slice(1);
        }
    } catch {}
    return raw;
}

function registerLumeoTheme() {
    if (lumeoThemeRegistered || !window.echarts) return;

    const fg = getCssVar('--color-foreground') || '#1a1a1a';
    const mutedFg = getCssVar('--color-muted-foreground') || '#737373';
    const border = getCssVar('--color-border') || '#e5e5e5';
    const bg = getCssVar('--color-background') || '#ffffff';
    const card = getCssVar('--color-card') || '#ffffff';

    const chart1 = getCssVar('--color-chart-1') || getCssVar('--color-primary') || '#e85d04';
    const chart2 = getCssVar('--color-chart-2') || '#2c9e8f';
    const chart3 = getCssVar('--color-chart-3') || '#2d4f5c';
    const chart4 = getCssVar('--color-chart-4') || '#d4a843';
    const chart5 = getCssVar('--color-chart-5') || '#e08844';

    const radiusRaw = getCssVar('--radius') || '0.75rem';
    const radiusPx = parseFloat(radiusRaw) * (radiusRaw.includes('rem') ? 16 : 1);
    const barRadius = [radiusPx, radiusPx, 0, 0];

    const noStroke = { textBorderWidth: 0, textBorderColor: 'transparent', textShadowBlur: 0, textShadowColor: 'transparent' };
    const labelNoStroke = { ...noStroke, color: mutedFg, fontSize: 11 };

    window.echarts.registerTheme('lumeo', {
        color: [chart1, chart2, chart3, chart4, chart5],
        backgroundColor: 'transparent',
        textStyle: {
            color: mutedFg,
            fontFamily: getComputedStyle(document.body).fontFamily || 'system-ui, sans-serif',
            fontSize: 12,
            ...noStroke
        },
        title: {
            textStyle: { color: fg, fontWeight: 600, fontSize: 14 },
            subtextStyle: { color: mutedFg, fontSize: 12 }
        },
        legend: {
            textStyle: { color: mutedFg, fontSize: 11, ...noStroke },
            icon: radiusPx > 0 ? 'roundRect' : 'rect',
            itemWidth: 12,
            itemHeight: 8,
            itemGap: 16
        },
        tooltip: {
            backgroundColor: card,
            borderColor: border,
            borderWidth: 1,
            textStyle: { color: fg, fontSize: 12 },
            extraCssText: `border-radius: ${radiusPx}px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); padding: 8px 12px;`
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
            }
        },
        label: labelNoStroke,
        line: {
            smooth: true,
            symbolSize: 0,
            lineStyle: { width: 2 },
            label: labelNoStroke
        },
        bar: {
            barMaxWidth: 32,
            itemStyle: { borderRadius: barRadius },
            label: labelNoStroke
        },
        pie: {
            itemStyle: { borderColor: card, borderWidth: 2 },
            label: labelNoStroke
        },
        radar: {
            axisName: { color: mutedFg, fontSize: 11 },
            splitLine: { lineStyle: { color: border, opacity: 0.4 } },
            splitArea: { areaStyle: { color: ['transparent', 'transparent'] } },
            axisLine: { lineStyle: { color: border, opacity: 0.3 } },
            label: labelNoStroke
        },
        scatter: {
            symbolSize: 8,
            itemStyle: { opacity: 0.75 },
            label: labelNoStroke
        },
        graph: {
            lineStyle: { color: border, opacity: 0.6 },
            label: labelNoStroke
        },
        sankey: {
            label: labelNoStroke
        },
        funnel: {
            label: labelNoStroke
        },
        treemap: {
            label: labelNoStroke
        },
        sunburst: {
            label: labelNoStroke
        },
        tree: {
            label: labelNoStroke
        },
        themeRiver: {
            label: labelNoStroke
        },
        heatmap: {
            label: labelNoStroke
        },
        boxplot: {
            label: labelNoStroke
        },
        candlestick: {
            label: labelNoStroke
        },
        parallel: {
            label: labelNoStroke
        },
        gauge: {
            axisLine: { lineStyle: { color: [[1, border]] } },
            axisTick: { show: false },
            splitLine: { show: false },
            axisLabel: { color: mutedFg, ...noStroke },
            detail: { color: fg, fontWeight: 600, ...noStroke },
            title: { color: mutedFg, ...noStroke }
        }
    });

    lumeoThemeRegistered = true;
}

export async function initChart(elementId, optionsJson, theme, echartsSource) {
    await loadECharts(echartsSource);

    const el = document.getElementById(elementId);
    if (!el) return;

    // Dispose existing instance if any
    if (charts.has(elementId)) {
        charts.get(elementId).dispose();
    }

    // Register and use Lumeo theme unless a specific theme is requested
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

    chart.setOption(options);
    charts.set(elementId, chart);

    // Auto-resize on container resize
    const observer = new ResizeObserver(() => {
        chart.resize();
    });
    observer.observe(el);
    chart._lumeoObserver = observer;
}

export function updateChart(elementId, optionsJson, notMerge) {
    const chart = charts.get(elementId);
    if (!chart) return;
    const options = JSON.parse(optionsJson);
    chart.setOption(options, { notMerge: notMerge || false });
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

// Re-register theme and refresh all charts (call when theme changes)
export function refreshAllCharts() {
    lumeoThemeRegistered = false;
    registerLumeoTheme();
    for (const [id, chart] of charts) {
        const el = document.getElementById(id);
        if (!el) continue;
        const opts = chart.getOption();
        chart.dispose();
        const newChart = window.echarts.init(el, 'lumeo', { renderer: 'canvas' });
        newChart.setOption(opts);
        charts.set(id, newChart);
    }
}

export function registerChartEvent(elementId, eventName, dotnetRef) {
    const chart = charts.get(elementId);
    if (!chart) return;
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

export async function loadExtension(url) {
    if (document.querySelector(`script[src="${url}"]`)) return;
    await loadECharts(); // ensure echarts is loaded first
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = url;
        script.onload = resolve;
        script.onerror = () => reject(new Error(`Failed to load extension: ${url}`));
        document.head.appendChild(script);
    });
}

export async function registerMap(mapName, geoJson) {
    await loadECharts();
    if (!window.echarts) return;
    const json = typeof geoJson === 'string' ? JSON.parse(geoJson) : geoJson;
    window.echarts.registerMap(mapName, json);
}
