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
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
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

    window.echarts.registerTheme('lumeo', {
        color: [chart1, chart2, chart3, chart4, chart5],
        backgroundColor: 'transparent',
        textStyle: {
            color: mutedFg,
            fontFamily: getComputedStyle(document.body).fontFamily || 'system-ui, sans-serif',
            fontSize: 12
        },
        title: {
            textStyle: { color: fg, fontWeight: 600, fontSize: 14 },
            subtextStyle: { color: mutedFg, fontSize: 12 }
        },
        legend: {
            textStyle: { color: mutedFg, fontSize: 11 },
            icon: 'roundRect',
            itemWidth: 12,
            itemHeight: 8,
            itemGap: 16
        },
        tooltip: {
            backgroundColor: card,
            borderColor: border,
            borderWidth: 1,
            textStyle: { color: fg, fontSize: 12 },
            extraCssText: 'border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); padding: 8px 12px;'
        },
        categoryAxis: {
            axisLine: { show: false },
            axisTick: { show: false },
            axisLabel: { color: mutedFg, fontSize: 11 },
            splitLine: { show: false }
        },
        valueAxis: {
            axisLine: { show: false },
            axisTick: { show: false },
            axisLabel: { color: mutedFg, fontSize: 11 },
            splitLine: {
                show: true,
                lineStyle: { color: border, type: 'dashed', opacity: 0.5 }
            }
        },
        line: {
            smooth: true,
            symbolSize: 0,
            lineStyle: { width: 2 }
        },
        bar: {
            barMaxWidth: 32,
            itemStyle: { borderRadius: [4, 4, 0, 0] }
        },
        pie: {
            itemStyle: { borderColor: card, borderWidth: 2 },
            label: { color: mutedFg, fontSize: 11 }
        },
        radar: {
            axisName: { color: mutedFg, fontSize: 11 },
            splitLine: { lineStyle: { color: border, opacity: 0.4 } },
            splitArea: { areaStyle: { color: ['transparent', 'transparent'] } },
            axisLine: { lineStyle: { color: border, opacity: 0.3 } }
        },
        scatter: {
            symbolSize: 8,
            itemStyle: { opacity: 0.75 }
        },
        graph: {
            lineStyle: { color: border, opacity: 0.6 }
        },
        gauge: {
            axisLine: { lineStyle: { color: [[1, border]] } },
            axisTick: { show: false },
            splitLine: { show: false },
            axisLabel: { color: mutedFg },
            detail: { color: fg, fontWeight: 600 },
            title: { color: mutedFg }
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
