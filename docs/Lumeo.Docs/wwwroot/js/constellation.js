// Lumeo Component Constellation — JS-driven hover & filter to avoid Blazor re-render storms.
// Owns the SVG once Blazor mounts an empty container; the entire star-map is rendered
// in vanilla JS so 154 dots don't go through a Blazor diff on every mouseover.
//
// Public API on window.lumeoConstellation:
//   init(rootEl, nodes, clusters, categoryColors)  → builds SVG + wires events
//   filter(needle)                                 → updates matched state for all dots
//   reset()                                        → tears down listeners + clears DOM
//
// Hover & tap states never mutate Blazor — the tooltip card is positioned with
// transform: translate(x, y) using getBoundingClientRect of the actual dot, so the
// tooltip lines up regardless of SVG aspect-ratio letterboxing.
//
// All variable content is set via textContent (no innerHTML on untrusted data).

(function () {
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const VIEW_W = 1000;
    const VIEW_H = 560;

    let state = null;

    // Treat "mobile" as anything without true hover (touch-primary device) OR
    // a narrow viewport. The hover check is the important one — a 1280px iPad
    // in landscape still has no real hover and should use tap-to-pin instead
    // of click-to-navigate.
    function isMobile() {
        if (!window.matchMedia) return false;
        return window.matchMedia('(hover: none)').matches
            || window.matchMedia('(pointer: coarse)').matches
            || window.matchMedia('(max-width: 1023px)').matches;
    }

    function teardown() {
        if (!state) return;
        if (state.root) state.root.innerHTML = '';
        if (state.tooltip && state.tooltip.parentNode) state.tooltip.parentNode.removeChild(state.tooltip);
        if (state.docHandlers) {
            document.removeEventListener('click', state.docHandlers.docClick, true);
            document.removeEventListener('touchstart', state.docHandlers.docTouch, true);
        }
        state = null;
    }

    function buildTooltip(host) {
        const tip = document.createElement('div');
        tip.className = 'lumeo-constellation-tip';
        tip.setAttribute('role', 'tooltip');
        tip.setAttribute('aria-hidden', 'true');
        // Visibility is toggled via the .is-visible class (defined in app.css)
        // rather than inline opacity — manipulating inline opacity through the
        // CSS transition pipeline turned out to be unreliable in Chrome's
        // automation harness (computed opacity stuck at 0 even when inline was
        // 1). Class-based show/hide is deterministic.
        tip.style.cssText = [
            'position:absolute',
            'top:0',
            'left:0',
            'pointer-events:none',
            'z-index:30',
            'max-width:230px',
            'padding:0.75rem',
            'border-radius:0.75rem',
            // Lumeo's Tailwind-v4 theme exposes colors as --color-card etc.
            // (the bare --card variable does NOT exist), so we use var()
            // directly rather than the hsl() function wrapper.
            'border:1px solid var(--color-border)',
            'background:var(--color-card)',
            'box-shadow:0 16px 32px -8px rgba(0,0,0,0.55)',
            'transform:translate3d(-9999px,-9999px,0)',
            'will-change:transform,opacity'
        ].join(';');

        const title = document.createElement('div');
        title.className = 'lumeo-tip-title';
        title.style.cssText = 'font-size:0.78rem;font-weight:600;color:var(--color-foreground);line-height:1.2;';
        tip.appendChild(title);

        const summary = document.createElement('div');
        summary.className = 'lumeo-tip-summary';
        summary.style.cssText = 'font-size:0.72rem;color:var(--color-muted-foreground);margin-top:0.3rem;line-height:1.4;';
        tip.appendChild(summary);

        const catRow = document.createElement('div');
        catRow.style.cssText = 'display:flex;align-items:center;gap:0.35rem;margin-top:0.5rem;';
        const swatch = document.createElement('span');
        swatch.className = 'lumeo-tip-swatch';
        swatch.style.cssText = 'display:inline-block;height:0.4rem;width:0.4rem;border-radius:9999px;';
        const catLabel = document.createElement('span');
        catLabel.className = 'lumeo-tip-cat';
        catLabel.style.cssText = 'font-size:0.66rem;color:var(--color-muted-foreground);';
        catRow.appendChild(swatch);
        catRow.appendChild(catLabel);
        tip.appendChild(catRow);

        // Visit button — hidden on desktop via CSS (hover-click on the dot
        // already navigates there). Visible when the tooltip is pinned on
        // mobile, where users need an explicit CTA.
        const visitBtn = document.createElement('a');
        visitBtn.className = 'lumeo-tip-visit';
        visitBtn.setAttribute('role', 'button');
        visitBtn.style.cssText = [
            'display:flex',
            'align-items:center',
            'justify-content:center',
            'gap:0.35rem',
            'margin-top:0.65rem',
            'padding:0.5rem 0.65rem',
            'font-size:0.74rem',
            'font-weight:600',
            'border-radius:0.5rem',
            'background:var(--color-primary)',
            'color:var(--color-primary-foreground)',
            'text-decoration:none',
            'pointer-events:auto',
            'cursor:pointer'
        ].join(';');
        visitBtn.textContent = 'Open docs →';
        tip.appendChild(visitBtn);

        host.appendChild(tip);
        return {
            root: tip,
            title: title,
            summary: summary,
            swatch: swatch,
            catLabel: catLabel,
            visitBtn: visitBtn
        };
    }

    function showTooltip(node, dotEl) {
        if (!state || !state.tooltip) return;
        const root = state.root;
        const rootRect = root.getBoundingClientRect();
        const dotRect = dotEl.getBoundingClientRect();
        const cx = dotRect.left + dotRect.width / 2 - rootRect.left;
        const cy = dotRect.top + dotRect.height / 2 - rootRect.top;

        const cat = node.category || 'Component';
        const color = (state.categoryColors && state.categoryColors[cat]) || 'var(--color-primary)';

        // Update content via textContent only — no HTML injection
        const t = state.tooltip;
        t.title.textContent = node.title || '';
        t.summary.textContent = node.summary || '';
        t.swatch.style.background = color;
        t.catLabel.textContent = cat;
        if (t.visitBtn && node.url) t.visitBtn.setAttribute('href', node.url);

        const tipEl = t.root;
        // Make the tip measurable but invisible by moving offscreen first.
        tipEl.style.transform = 'translate3d(-9999px,-9999px,0)';
        tipEl.classList.add('is-visible');
        const tw = tipEl.offsetWidth;
        const th = tipEl.offsetHeight;

        const PAD = 10;
        let tx = cx - tw / 2;
        let ty = cy - th - PAD;
        if (ty < 0) ty = cy + PAD;
        if (tx < 4) tx = 4;
        if (tx + tw > rootRect.width - 4) tx = rootRect.width - tw - 4;

        tipEl.style.transform = 'translate3d(' + tx + 'px,' + ty + 'px,0)';
        tipEl.setAttribute('aria-hidden', 'false');
        state.activeDot = dotEl;
    }

    function hideTooltip() {
        if (!state || !state.tooltip) return;
        state.tooltip.root.classList.remove('is-visible');
        state.tooltip.root.setAttribute('aria-hidden', 'true');
        state.activeDot = null;
    }

    function applyFilter(needle) {
        if (!state) return 0;
        needle = (needle || '').trim().toLowerCase();
        let matchCount = 0;
        for (let i = 0; i < state.dots.length; i++) {
            const dot = state.dots[i];
            const titleLower = (dot._title || '').toLowerCase();
            const isMatch = needle.length === 0 || titleLower.indexOf(needle) >= 0;
            if (isMatch) matchCount++;
            const attr = (needle.length === 0 || isMatch) ? '1' : '0';
            if (dot.getAttribute('data-matched') !== attr) {
                dot.setAttribute('data-matched', attr);
            }
        }
        return matchCount;
    }

    function init(rootEl, nodes, clusters, categoryColors) {
        if (!rootEl) return;
        teardown();

        rootEl.innerHTML = '';

        const svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');
        svg.setAttribute('viewBox', '0 0 ' + VIEW_W + ' ' + VIEW_H);
        svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
        svg.setAttribute('role', 'list');
        svg.setAttribute('aria-label', 'Lumeo component constellation');
        svg.style.display = 'block';
        svg.style.width = '100%';
        svg.style.height = '100%';

        if (clusters && categoryColors) {
            Object.keys(clusters).forEach(function (key) {
                const c = clusters[key];
                const t = document.createElementNS(SVG_NS, 'text');
                t.setAttribute('x', c.cx.toFixed(1));
                // `dominant-baseline="hanging"` puts y at the TOP of the text
                // (instead of the alphabetic baseline). Without it, ascenders
                // overshoot the viewBox top and labels like "Navigation" get
                // clipped at clusters near y=0. Also clamp y so the label is
                // never closer than 4px to the top edge.
                const yPos = Math.max(4, c.cy - c.r - 16);
                t.setAttribute('y', yPos.toFixed(1));
                t.setAttribute('dominant-baseline', 'hanging');
                t.setAttribute('text-anchor', 'middle');
                t.setAttribute('font-size', '11');
                t.setAttribute('font-family', 'ui-sans-serif,system-ui,sans-serif');
                t.setAttribute('font-weight', '600');
                t.setAttribute('letter-spacing', '0.04em');
                t.setAttribute('fill', categoryColors[key] || 'currentColor');
                t.setAttribute('opacity', '0.7');
                t.classList.add('lumeo-constellation-label');
                t.style.pointerEvents = 'none';
                t.style.textShadow = '0 0 4px var(--color-background)';
                t.textContent = key;
                svg.appendChild(t);
            });
        }

        const dots = [];
        nodes.forEach(function (node) {
            const c = document.createElementNS(SVG_NS, 'circle');
            const color = (categoryColors && categoryColors[node.category]) || 'var(--color-primary)';
            c.setAttribute('cx', Number(node.x).toFixed(1));
            c.setAttribute('cy', Number(node.y).toFixed(1));
            c.setAttribute('r', '5');
            c.setAttribute('fill', color);
            c.setAttribute('role', 'link');
            c.setAttribute('tabindex', '0');
            c.setAttribute('aria-label', node.title + ', ' + (node.category || ''));
            c.setAttribute('data-id', node.id);
            c.setAttribute('data-url', node.url);
            c.setAttribute('data-matched', '1');
            c.style.cursor = 'pointer';
            c._node = node;
            c._title = node.title;
            svg.appendChild(c);
            dots.push(c);
        });

        rootEl.appendChild(svg);

        const tooltip = buildTooltip(rootEl);
        state = {
            root: rootEl,
            svg: svg,
            dots: dots,
            tooltip: tooltip,
            categoryColors: categoryColors,
            activeDot: null,
            pinned: false
        };

        svg.addEventListener('mouseover', function (e) {
            if (state.pinned) return;
            const t = e.target.closest('circle[data-id]');
            if (!t) return;
            showTooltip(t._node, t);
        });
        svg.addEventListener('mouseout', function (e) {
            if (state.pinned) return;
            const t = e.target.closest('circle[data-id]');
            if (!t) return;
            const related = e.relatedTarget;
            if (related && svg.contains(related)) return;
            hideTooltip();
        });
        svg.addEventListener('mouseleave', function () {
            if (!state.pinned) hideTooltip();
        });
        svg.addEventListener('click', function (e) {
            const t = e.target.closest('circle[data-id]');
            if (!t) return;
            if (isMobile()) {
                // Mobile/touch: tap NEVER navigates from a dot — it only pins
                // the tooltip. The user navigates via the "Open docs" button
                // inside the tooltip. This avoids accidental double-tap
                // navigation and keeps the UX consistent: one tap = preview,
                // explicit button tap = commit.
                e.preventDefault();
                state.pinned = true;
                showTooltip(t._node, t);
            } else {
                // Desktop with hover: a click on the dot navigates immediately,
                // since the user has already seen the tooltip on hover.
                window.location.assign(t.getAttribute('data-url'));
            }
        });
        svg.addEventListener('keydown', function (e) {
            const t = document.activeElement;
            if (!t || !t.matches || !t.matches('circle[data-id]')) return;
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                window.location.assign(t.getAttribute('data-url'));
            }
        });
        svg.addEventListener('focusin', function (e) {
            const t = e.target.closest('circle[data-id]');
            if (!t) return;
            showTooltip(t._node, t);
        });
        svg.addEventListener('focusout', function () {
            if (!state.pinned) hideTooltip();
        });

        const docClick = function (e) {
            if (!state || !state.pinned) return;
            if (!svg.contains(e.target)) {
                state.pinned = false;
                hideTooltip();
            }
        };
        document.addEventListener('click', docClick, true);
        document.addEventListener('touchstart', docClick, true);
        state.docHandlers = { docClick: docClick, docTouch: docClick };
    }

    window.lumeoConstellation = {
        init: init,
        filter: applyFilter,
        reset: teardown
    };
})();
