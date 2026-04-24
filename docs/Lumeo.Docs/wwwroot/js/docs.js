window.lumeo = window.lumeo || {};

// Set by MainLayout after first render completes. The prerender crawler
// (scripts/prerender/prerender.mjs) waits on this data attribute before
// snapshotting the DOM. No-op for real users — the attribute is never read
// at runtime.
window.lumeo.signalBlazorReady = function () {
    document.documentElement.dataset.blazorReady = 'true';
};

window.lumeo.setupSearch = function () {
    document.addEventListener('keydown', function (e) {
        if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
            e.preventDefault();
            document.dispatchEvent(new CustomEvent('lumeo-open-search'));
        }
    });
    document.addEventListener('lumeo-open-search', function () {
        var btn = document.querySelector('[data-search-trigger]');
        if (btn) btn.click();
    });
};
