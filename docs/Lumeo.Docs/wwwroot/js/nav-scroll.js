// Scrolls the active NavLink inside the docs sidebar into view. Called from
// NavMenu.razor on initial render + every LocationChanged so direct-link loads
// and in-app navigation both land with the current page's entry visible.
window.lumeoNavScrollActiveIntoView = function (navEl) {
    if (!navEl) return;
    // Blazor's NavLink adds the "active" class on the anchor that matches the current URL.
    const active = navEl.querySelector('a.active');
    if (!active) return;
    // scrollIntoView with block: 'nearest' only scrolls when actually needed (no jumpy
    // behaviour on already-visible items) and stays inside the scrollable <aside>.
    active.scrollIntoView({ block: 'nearest', inline: 'nearest' });
};
