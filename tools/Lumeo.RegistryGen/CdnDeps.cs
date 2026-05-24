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
        // echarts-interop.js: window.lumeoCdn.echarts
        new("echarts",                 "echarts",                  "5",       "https://cdn.jsdelivr.net/npm/echarts@5/dist/echarts.min.js",           "Lumeo.Charts"),

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
