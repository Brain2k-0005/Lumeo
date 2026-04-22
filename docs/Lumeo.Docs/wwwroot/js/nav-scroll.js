// Scrolls the active NavLink inside the docs sidebar into view. Called from
// NavMenu.razor on initial render + every LocationChanged so direct-link loads
// and in-app navigation both land with the current page's entry visible.
window.lumeoNavScrollActiveIntoView = function (navEl) {
    if (!navEl) return;
    // Blazor's NavLink adds the "active" class on the anchor that matches the current URL.
    const active = navEl.querySelector('a.active');
    if (!active) return;

    // The scrollable ancestor is the <aside> (the nav is its child). scrollIntoView
    // with block: 'nearest' was dropping the active anchor at the BOTTOM of the viewport
    // because it only considered the nav element's box, not the aside's scroll box.
    // We compute the offset manually and set the aside's scrollTop to center the active.
    const scrollParent = navEl.closest('aside') || navEl.parentElement;
    if (!scrollParent) return;

    const navRect = scrollParent.getBoundingClientRect();
    const activeRect = active.getBoundingClientRect();
    const currentScroll = scrollParent.scrollTop;
    // Position within the scroll container, then offset so the active item is centered.
    const activeTopInScroll = (activeRect.top - navRect.top) + currentScroll;
    const desiredScroll = activeTopInScroll - (scrollParent.clientHeight / 2) + (activeRect.height / 2);

    scrollParent.scrollTop = Math.max(0, desiredScroll);
};
