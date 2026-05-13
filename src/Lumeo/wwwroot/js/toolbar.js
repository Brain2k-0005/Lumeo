// toolbar.js — ResizeObserver helper for Toolbar overflow-collapse.
// Observes a toolbar container element and notifies a .NET DotNetObjectReference
// each time the available width changes so the Blazor component can recompute
// how many items fit before overflowing into the "⋯" dropdown.

const _overflowObservers = new Map(); // elementId → { observer, dotnetRef, callbackName, lastFitting }

/**
 * Observe a toolbar element. On every resize, measure child widths, compute
 * how many fit before the overflow trigger, and invoke the .NET callback with
 * (fittingCount, totalCount). This is what Toolbar.razor's [JSInvokable]
 * `OnOverflowMeasured` expects.
 *
 * Up to rc.41 the only exported observe-fn was the legacy `observeToolbarWidth`
 * which fired raw widths and expected the .NET side to do the math — but
 * Toolbar.razor was already calling `observeToolbarOverflow` (this function),
 * so the entire overflow feature was failing silently at runtime with "function
 * does not exist". Fixed in rc.42.
 *
 * @param {string} elementId  id of the toolbar container
 * @param {object} dotnetRef  DotNetObjectReference<Toolbar>
 * @param {string} callbackName  invoke method name (e.g. "OnOverflowMeasured")
 */
export function observeToolbarOverflow(elementId, dotnetRef, callbackName) {
    disposeToolbarOverflow(elementId);

    const el = document.getElementById(elementId);
    if (!el) return;

    const compute = () => {
        const state = _overflowObservers.get(elementId);
        if (!state) return;

        const containerWidth = el.getBoundingClientRect().width;
        // If the overflow trigger is present, reserve its width so chips don't
        // collide with it. The trigger is tagged `data-toolbar-overflow-trigger`.
        const trigger = el.querySelector('[data-toolbar-overflow-trigger]');
        const triggerWidth = trigger ? trigger.getBoundingClientRect().width : 0;
        const available = containerWidth - triggerWidth - 4; // 4px buffer for gap-1

        const children = Array.from(el.children).filter(c => !c.hasAttribute('data-toolbar-overflow-trigger'));
        const total = children.length;

        let used = 0;
        let fitting = total;
        for (let i = 0; i < total; i++) {
            const w = children[i].getBoundingClientRect().width + 4; // include gap-1
            if (used + w > available) {
                fitting = i;
                break;
            }
            used += w;
        }

        if (fitting !== state.lastFitting) {
            state.lastFitting = fitting;
            dotnetRef.invokeMethodAsync(callbackName, fitting, total).catch(() => {});
        }
    };

    const observer = new ResizeObserver(() => compute());
    observer.observe(el);

    _overflowObservers.set(elementId, { observer, dotnetRef, callbackName, lastFitting: -1 });
    // First measurement so the component doesn't wait for a resize event.
    compute();
}

/**
 * Stop observing and clean up. Safe to call with an unknown elementId.
 * @param {string} elementId
 */
export function disposeToolbarOverflow(elementId) {
    const state = _overflowObservers.get(elementId);
    if (state) {
        state.observer.disconnect();
        _overflowObservers.delete(elementId);
    }
}

// ── Legacy API (raw-width observer). Kept for backward compat with any
//    external code that imports observeToolbarWidth / measureToolbarChildren
//    directly. Toolbar.razor itself uses the observeToolbarOverflow path above.
const _observers = new Map(); // elementId → ResizeObserver

/**
 * Start observing `elementId` for width changes.
 * @param {string} elementId
 * @param {object} dotnetRef  DotNetObjectReference<Toolbar> with [JSInvokable] OnWidthChanged
 */
export function observeToolbarWidth(elementId, dotnetRef) {
    // Disconnect any previous observer for the same id
    disposeToolbarObserver(elementId);

    const el = document.getElementById(elementId);
    if (!el) return;

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const width = entry.contentRect.width;
            dotnetRef.invokeMethodAsync('OnWidthChanged', width).catch(() => {});
        }
    });

    observer.observe(el);
    _observers.set(elementId, observer);

    // Fire immediately with the current width so the component doesn't have
    // to wait for a resize event to do its first layout pass.
    const rect = el.getBoundingClientRect();
    dotnetRef.invokeMethodAsync('OnWidthChanged', rect.width).catch(() => {});
}

/**
 * Measure the natural (unconstrained) width of each direct child of the toolbar.
 * Returns an array of pixel widths in DOM order.
 * @param {string} elementId
 * @returns {number[]}
 */
export function measureToolbarChildren(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return [];

    // Children that are NOT the overflow button (we tag it with data-overflow-btn)
    const children = Array.from(el.children).filter(c => !c.hasAttribute('data-overflow-btn'));
    return children.map(c => {
        const rect = c.getBoundingClientRect();
        // Include gap (4px gap-1 = 0.25rem). Tailwind gap-1 = 4px.
        return rect.width + 4;
    });
}

/**
 * Stop observing the element and clean up.
 * @param {string} elementId
 */
export function disposeToolbarObserver(elementId) {
    const observer = _observers.get(elementId);
    if (observer) {
        observer.disconnect();
        _observers.delete(elementId);
    }
}
