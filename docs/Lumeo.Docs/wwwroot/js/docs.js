window.lumeo = window.lumeo || {};

// Set by MainLayout after first render completes. The prerender crawler
// (scripts/prerender/prerender.mjs) waits on this data attribute before
// snapshotting the DOM. No-op for real users — the attribute is never read
// at runtime.
window.lumeo.signalBlazorReady = function () {
    document.documentElement.dataset.blazorReady = 'true';

    // Landing (deferred hydration): the '/' snapshot has no full-screen splash,
    // it is readable marketing content. Drop the tiny "Starting…" boot pill and
    // the pending state that neutralised the static header's not-yet-live
    // <button> controls — the app has now re-rendered #app and is interactive.
    document.documentElement.classList.remove('lumeo-landing-pending');
    var pill = document.querySelector('.lumeo-boot-pill');
    if (pill) pill.remove();

    // Other routes: reveal the app. A full-screen boot splash is injected into
    // the prerendered HTML (see scripts/prerender/prerender.mjs); it stays up
    // while the WASM runtime downloads, boots and re-renders #app from scratch,
    // so users never see the dead pre-hydration DOM (empty data, flashing
    // consent banner) or the hydration re-render. We drop it here — the exact
    // moment MainLayout's first interactive render completes.
    var splash = document.querySelector('.lumeo-splash');
    if (splash) {
        splash.classList.add('lumeo-splash--hide');
        setTimeout(function () { splash.remove(); }, 450);
    }
};

// True only inside the prerender crawler (set via evaluateOnNewDocument in
// scripts/prerender/prerender.mjs). Components use this to render below-the-fold,
// IntersectionObserver-gated content eagerly during prerender — the headless
// crawler doesn't scroll, so those observers never fire — while keeping the lazy
// path for real users. Always false at runtime.
window.lumeo.isPrerender = function () {
    return !!window.__LUMEO_PRERENDER__;
};

// Returns the registry JSON inlined into the catalog's prerendered HTML
// (<script id="lumeo-registry-data">), or null. RegistryService reads this
// synchronously on hydration so the catalog renders populated on its first
// pass — no skeleton swap, no layout shift. Null on client-side navigation
// (the script only exists in the prerendered /components document).
window.lumeo.readInlineRegistry = function () {
    var el = document.getElementById('lumeo-registry-data');
    return el ? el.textContent : null;
};

// Scroll a heading into view from the OnThisPage sidebar. Using a dedicated
// function instead of eval() avoids CSP issues and keeps interop auditable.
// Also reflects the section into the URL hash so right-click "copy link"
// and browser-share produce a deep link like /components/datagrid#full-featured-datagrid
// instead of the bare route. replaceState (not pushState) keeps the back
// button focused on real route changes rather than TOC scrolls.
//
// IMPORTANT: build the URL absolutely, not as bare '#id'. index.html sets
// <base href="/">, and per the HTML spec the third argument to replaceState
// is parsed against the document's *API base URL* (which reflects <base>),
// so passing '#id' resolves to '/#id' — dropping users on the bare domain
// when they later copied the URL out of a component page. Issue #41
// reported this. Concatenating pathname + search + '#id' bypasses the
// base-href resolution and writes the URL the comment above promises.
window.lumeo.navScrollActiveIntoView = function (id) {
    var el = document.getElementById(id);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        try {
            var pathQuery = (window.location.pathname || '/') + (window.location.search || '');
            history.replaceState(null, '', pathQuery + '#' + id);
        } catch (_) { /* SSR / sandboxed */ }
    }
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

// PauseWhenHidden — PERSISTENT IntersectionObserver (unlike observeVisibility,
// which fires once and disconnects). Calls dotNetRef.SetVisible(bool) on every
// enter/leave so the caller can pause perpetual work (live timers, chart
// updates, CSS animations) while the element is off-screen and resume it when
// it scrolls back. Returns the observer so Blazor can disconnect on dispose.
window.lumeo.observeInViewport = function (el, dotNetRef, methodName, rootMarginPx) {
    var method = methodName || 'SetVisible';
    if (!el || typeof IntersectionObserver === 'undefined') {
        // No observer support (SSR / bUnit): treat as always visible so nothing
        // that depends on visibility is starved.
        try { dotNetRef.invokeMethodAsync(method, true); } catch (_) {}
        return null;
    }
    var io = new IntersectionObserver(function (entries) {
        var visible = entries[entries.length - 1].isIntersecting;
        try { dotNetRef.invokeMethodAsync(method, visible); } catch (_) {}
    }, { rootMargin: (rootMarginPx || 0) + 'px' });
    io.observe(el);
    return io;
};

// prefers-reduced-motion query for the C# side. The landing page uses it to
// suppress its perpetual liveness (chart reshuffle timer) entirely for users
// who asked the OS to reduce motion — CSS-only animations are already gated in
// lumeo.css, this covers the JS/timer-driven ones.
window.lumeo.prefersReducedMotion = function () {
    return typeof window !== 'undefined'
        && typeof window.matchMedia === 'function'
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
};

// Idle-mount hook for IdleMount.razor. Keeps a heavy above-the-fold component
// (e.g. the hero ECharts BarChart) out of the first-paint critical path: we
// show a cheap static placeholder immediately, then swap in the real component
// once the browser is idle (past the WASM boot + first interactive render).
// During prerender we fire immediately so the static snapshot has real content.
window.lumeo.onIdle = function (dotNetRef, timeoutMs) {
    if (window.__LUMEO_PRERENDER__) {
        dotNetRef.invokeMethodAsync('OnIdle');
        return;
    }
    var fire = function () { dotNetRef.invokeMethodAsync('OnIdle'); };
    if (typeof window.requestIdleCallback === 'function') {
        window.requestIdleCallback(fire, { timeout: timeoutMs || 2000 });
    } else {
        // Safari/older: defer past the current paint with a small timeout.
        setTimeout(fire, 200);
    }
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

// Swipable tabs for the mobile Navigation drawer.
// Pages are stacked horizontally inside .lumeo-mobile-nav-pages with
// scroll-snap; tab buttons programmatically scroll to their section, and
// the scroll listener updates the active tab indicator as the user swipes.
window.lumeo.bindMobileNavTabs = function (host) {
    if (!host) return;
    const nav = host.querySelector('.lumeo-mobile-nav-tabs');
    const pages = host.querySelector('.lumeo-mobile-nav-pages');
    if (!nav || !pages) return;

    const tabs = Array.from(nav.querySelectorAll('.lumeo-mobile-nav-tab'));
    const sections = Array.from(pages.querySelectorAll('.lumeo-mobile-nav-page'));

    function setActive(idx) {
        tabs.forEach(function (b, i) {
            const on = i === idx;
            b.classList.toggle('is-active', on);
            b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
    }

    tabs.forEach(function (btn, i) {
        btn.addEventListener('click', function () {
            sections[i].scrollIntoView({ behavior: 'smooth', inline: 'start', block: 'nearest' });
            setActive(i);
        });
    });

    let scrollTimer = null;
    pages.addEventListener('scroll', function () {
        if (scrollTimer) clearTimeout(scrollTimer);
        scrollTimer = setTimeout(function () {
            const w = pages.clientWidth || 1;
            const idx = Math.round(pages.scrollLeft / w);
            setActive(Math.max(0, Math.min(idx, sections.length - 1)));
        }, 90);
    });

    setActive(0);
};
