namespace Lumeo.RegistryGen;

/// <summary>Single CDN dependency descriptor.</summary>
/// <param name="Key">The key used in <c>window.lumeoCdn[key]</c> — must match exactly what the JS interop reads.</param>
/// <param name="Package">npm package name (used as the sub-directory under <c>wwwroot/lib/lumeo-vendor/</c>).</param>
/// <param name="Version">Version string (semver major or full version).</param>
/// <param name="Url">Canonical CDN URL used as the fallback in JS interop.</param>
/// <param name="Owner">Satellite package that owns / consumes this dependency.</param>
public record CdnDep(string Key, string Package, string Version, string Url, string Owner);

/// <summary>
/// Single source of truth for all CDN JavaScript (and CSS) dependencies used
/// across Lumeo satellites.  Every entry here matches an <c>_cdn(key, fallback)</c>
/// call in the corresponding satellite's wwwroot/js/*.js file.
/// </summary>
public static class CdnDeps
{
    public static readonly CdnDep[] All =
    [
        // ── Lumeo.Maps ──────────────────────────────────────────────────────────
        // map.js: _cdn('mapLibreJs', ...) / _cdn('mapLibreCss', ...)
        new("mapLibreJs",              "maplibre-gl",              "5.7.1",   "https://unpkg.com/maplibre-gl@5.7.1/dist/maplibre-gl.js",              "Lumeo.Maps"),
        new("mapLibreCss",             "maplibre-gl",              "5.7.1",   "https://unpkg.com/maplibre-gl@5.7.1/dist/maplibre-gl.css",             "Lumeo.Maps"),

        // ── Lumeo.PdfViewer ─────────────────────────────────────────────────────
        // pdf-viewer.js: _cdn('pdfJs', ...) / _cdn('pdfJsWorker', ...)
        new("pdfJs",                   "pdfjs-dist",               "4.0.379", "https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.mjs",        "Lumeo.PdfViewer"),
        new("pdfJsWorker",             "pdfjs-dist",               "4.0.379", "https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.mjs", "Lumeo.PdfViewer"),

        // ── Lumeo.Charts ────────────────────────────────────────────────────────
        // echarts-interop.js: window.lumeoCdn.echarts (loadECharts) and the two plugin
        // override keys read by loadExtension(url, overrideKey) — LiquidFillChart and
        // WordCloudChart pass these so a host can self-host the plugin (GDPR: no pre-consent
        // CDN hit). Without them in the manifest, `lumeo deps install --write-bootstrap`
        // could vendor echarts but silently miss the plugins, so the bootstrap left them on
        // the public CDN. Versions match the component defaults (liquidfill@3, wordcloud@2.1.0).
        new("echarts",                 "echarts",                  "5",       "https://cdn.jsdelivr.net/npm/echarts@5/dist/echarts.min.js",                       "Lumeo.Charts"),
        new("echartsLiquidfill",       "echarts-liquidfill",       "3",       "https://cdn.jsdelivr.net/npm/echarts-liquidfill@3/dist/echarts-liquidfill.min.js", "Lumeo.Charts"),
        new("echartsWordcloud",        "echarts-wordcloud",        "2.1.0",   "https://cdn.jsdelivr.net/npm/echarts-wordcloud@2.1.0/dist/echarts-wordcloud.min.js", "Lumeo.Charts"),
        // NOT in the manifest by design: the world-map GeoJSON (echarts-map-world@4.9.0/
        // world.json). The manifest maps window.lumeoCdn[key] → a vendored script that the
        // interop auto-loads; the map JSON is neither. No interop reads a world-map key off
        // window.lumeoCdn — GeoMapChart takes the GeoJSON as its MapJson STRING parameter,
        // which the CONSUMER fetches from a URL of their own choosing and passes in. It is a
        // consumer-owned data asset, not a runtime-autoloaded dependency, so it has no
        // override key to vendor. (The docs site self-hosts its own copy under
        // wwwroot/lib/lumeo-vendor/ for its demo; that is a docs concern, not a CLI one.)

        // ── Lumeo.Scheduler ─────────────────────────────────────────────────────
        // scheduler.js: _cdn('fullCalendarCore' | 'fullCalendarDaygrid' | ...)
        new("fullCalendarCore",        "@fullcalendar/core",        "6",       "https://esm.sh/@fullcalendar/core@6",                                 "Lumeo.Scheduler"),
        new("fullCalendarDaygrid",     "@fullcalendar/daygrid",     "6",       "https://esm.sh/@fullcalendar/daygrid@6",                              "Lumeo.Scheduler"),
        new("fullCalendarTimegrid",    "@fullcalendar/timegrid",    "6",       "https://esm.sh/@fullcalendar/timegrid@6",                             "Lumeo.Scheduler"),
        new("fullCalendarList",        "@fullcalendar/list",        "6",       "https://esm.sh/@fullcalendar/list@6",                                 "Lumeo.Scheduler"),
        new("fullCalendarInteraction", "@fullcalendar/interaction", "6",       "https://esm.sh/@fullcalendar/interaction@6",                          "Lumeo.Scheduler"),

        // ── Lumeo.CodeEditor ────────────────────────────────────────────────────
        // code-editor.js: _cdn('codeMirrorBase', 'https://esm.sh')
        new("codeMirrorBase",          "@codemirror/*",             "6",       "https://esm.sh",                                                       "Lumeo.CodeEditor"),

        // ── Lumeo.Docs (docs-site only, not a NuGet satellite) ──────────────────
        // algolia-search.js: window.lumeoCdn.algoliasearchLite
        new("algoliasearchLite",       "algoliasearch",             "latest",  "https://esm.sh/algoliasearch/lite",                                    "Lumeo.Docs"),
    ];
}
