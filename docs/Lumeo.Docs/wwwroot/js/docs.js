window.lumeo = window.lumeo || {};

// Set by MainLayout after first render completes. The prerender crawler
// (scripts/prerender/prerender.mjs) waits on this data attribute before
// snapshotting the DOM. No-op for real users — the attribute is never read
// at runtime.
window.lumeo.signalBlazorReady = function () {
    document.documentElement.dataset.blazorReady = 'true';
};

// Scroll a heading into view from the OnThisPage sidebar. Using a dedicated
// function instead of eval() avoids CSP issues and keeps interop auditable.
window.lumeo.navScrollActiveIntoView = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

// Reset the scrollTop of an element by id. Avoids `eval()` for CSP compatibility.
window.lumeo.resetScrollTopById = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollTop = 0;
};

// LazyRender — IntersectionObserver helper.
// Watches `el` and calls dotNetRef.OnVisible() once it enters the viewport
// within `rootMarginPx` pixels. Returns the observer so Blazor can disconnect it.
window.lumeo.observeVisibility = function (el, dotNetRef, rootMarginPx) {
    if (!el || typeof IntersectionObserver === 'undefined') {
        // Fallback: fire immediately (SSR / older browsers).
        dotNetRef.invokeMethodAsync('OnVisible');
        return null;
    }
    var io = new IntersectionObserver(function (entries) {
        if (entries[0].isIntersecting) {
            io.disconnect();
            dotNetRef.invokeMethodAsync('OnVisible');
        }
    }, { rootMargin: rootMarginPx + 'px' });
    io.observe(el);
    return io;
};

window.lumeo.disconnectObserver = function (io) {
    if (io && typeof io.disconnect === 'function') io.disconnect();
};

// Page-visibility hook. The docs landing page runs several timers (chart
// reshuffle 2s, toast bank 11s, tabs/steps rotation). Background tabs keep
// firing the timers (and HTML5 spec throttles them but doesn't pause them),
// so when the user comes back from another tab the queued work bursts —
// toasts spam, charts cycle in fast-forward. We pause every timer while
// the tab is hidden by gating its callback on a single _pageVisible flag,
// kept in sync by this listener.
//
// Returns a disposer the caller invokes on component dispose.
window.lumeo.onPageVisibility = function (dotNetRef, methodName) {
    if (typeof document === 'undefined') return null;
    const fire = () => {
        try { dotNetRef.invokeMethodAsync(methodName, !document.hidden); } catch (_) {}
    };
    document.addEventListener('visibilitychange', fire);
    // Fire once on subscribe so the C# side has the initial state.
    fire();
    return {
        dispose: () => document.removeEventListener('visibilitychange', fire)
    };
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
