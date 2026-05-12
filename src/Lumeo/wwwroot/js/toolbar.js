// toolbar.js — ResizeObserver helper for Toolbar overflow-collapse.
// Observes a toolbar container element and notifies a .NET DotNetObjectReference
// each time the available width changes so the Blazor component can recompute
// how many items fit before overflowing into the "⋯" dropdown.

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
