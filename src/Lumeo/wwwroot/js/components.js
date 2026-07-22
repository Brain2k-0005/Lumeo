const clickOutsideHandlers = new Map();

// rc.44 — Mobile fix: on touch devices, `mousedown` is only emulated AFTER the
// touch sequence completes (and is often suppressed entirely on scrollable
// pages), so dropdown/popover/select etc. wouldn't dismiss on tap-outside.
// We listen to BOTH `mousedown` and `touchstart` and de-dupe via a tiny
// suppression window: when touchstart fires we set a flag, the synthetic
// mousedown that follows ~300ms later is ignored. `passive: true` on
// touchstart keeps scroll performance intact.
let _suppressNextMousedownUntil = 0;

const _clickOutsideHandler = (e) => {
    if (e.type === 'mousedown' && performance.now() < _suppressNextMousedownUntil) return;
    if (e.type === 'touchstart') {
        // Mark any mousedown in the next 600ms as a duplicate of this touch.
        _suppressNextMousedownUntil = performance.now() + 600;
    }
    // For touchstart, `e.target` is the touched element directly — same as mousedown.
    const target = e.target;
    for (const [id, { triggerElementId, dotnetRef }] of clickOutsideHandlers) {
        const el = document.getElementById(id);
        const trigger = triggerElementId ? document.getElementById(triggerElementId) : null;
        if (el && !el.contains(target) && (!trigger || !trigger.contains(target))) {
            dotnetRef.invokeMethodAsync('OnClickOutside', id);
        }
    }
};

document.addEventListener('mousedown', _clickOutsideHandler);
document.addEventListener('touchstart', _clickOutsideHandler, { passive: true });

// ----------------------------------------------------------------------------
// touch-action: none on every [draggable="true"] element. Without this, the
// mobile browser commits to a vertical-scroll interpretation of the touch
// gesture before our touchmove listener can call preventDefault, so the
// polyfill below never gets a chance to take over. We do it from JS rather
// than CSS so consumer apps that already ship a Lumeo CSS bundle pick it up
// automatically without having to add a global rule. Opt out with
// `data-no-touch-drag="true"` on the element itself or any ancestor.
(function injectDraggableTouchActionStyle() {
    if (document.getElementById('lumeo-draggable-touch-action')) return;
    const style = document.createElement('style');
    style.id = 'lumeo-draggable-touch-action';
    style.textContent =
        '[draggable="true"]:not([data-no-touch-drag="true"]):not([data-no-touch-drag="true"] *) ' +
        '{ touch-action: none; -webkit-user-select: none; user-select: none; }';
    (document.head || document.documentElement).appendChild(style);
})();

// ----------------------------------------------------------------------------
// Touch-to-drag polyfill. iOS Safari and Android Chrome do not fire dragstart /
// dragover / drop for `draggable="true"` elements on touch input, so every
// Blazor `@ondragstart` / `@ondrop` handler is silently desktop-only.
//
// This shim synthesises the HTML5 drag-and-drop event sequence from touch
// events so any [draggable="true"] element in a Lumeo (or consumer) page works
// the same on phone as on desktop. Active on the document — single-finger
// drags only; multitouch is left alone for pinch-zoom etc.
//
// Behaviour:
//   touchstart on a draggable → record source, wait for movement (dead-zone)
//   first touchmove past 8 px → dispatch `dragstart` on source
//   subsequent touchmoves     → dispatch `dragenter`/`dragleave`/`dragover`
//                               on the element under the finger
//   touchend                  → dispatch `drop` on that element, then
//                               `dragend` on the source
//
// Targets receive plain `Event` instances (not real `DragEvent`s). Blazor's
// `DragEventArgs` will carry empty DataTransfer, which is fine for the typical
// "remember which card is being dragged in C#" pattern. If you need real
// DataTransfer payloads on mobile, opt out via `data-no-touch-drag="true"`.
// ----------------------------------------------------------------------------
const TOUCH_DRAG_DEAD_ZONE_PX = 8;
let _touchDragSource = null;

const _onTouchDragMove = (e) => {
    if (!_touchDragSource || e.touches.length !== 1) return;
    const t = e.touches[0];
    const dx = t.clientX - _touchDragSource.startX;
    const dy = t.clientY - _touchDragSource.startY;

    if (!_touchDragSource.started) {
        if (Math.hypot(dx, dy) < TOUCH_DRAG_DEAD_ZONE_PX) return;
        _touchDragSource.started = true;
        _touchDragSource.element.dispatchEvent(new Event('dragstart', { bubbles: true, cancelable: true }));
    }

    // Stop the page from scrolling while a drag is in progress. We only call
    // preventDefault after the dead-zone is crossed so light taps and short
    // intentional scrolls aren't hijacked.
    e.preventDefault();

    const over = document.elementFromPoint(t.clientX, t.clientY);
    if (over !== _touchDragSource.lastOver) {
        if (_touchDragSource.lastOver) {
            _touchDragSource.lastOver.dispatchEvent(new Event('dragleave', { bubbles: true }));
        }
        if (over) {
            over.dispatchEvent(new Event('dragenter', { bubbles: true }));
        }
        _touchDragSource.lastOver = over;
    }
    if (over) {
        over.dispatchEvent(new Event('dragover', { bubbles: true, cancelable: true }));
    }
};

const _onTouchDragEnd = (e) => {
    if (!_touchDragSource) return;
    if (_touchDragSource.started) {
        const t = e.changedTouches && e.changedTouches[0];
        const dropTarget = t ? document.elementFromPoint(t.clientX, t.clientY) : null;
        if (dropTarget) {
            dropTarget.dispatchEvent(new Event('drop', { bubbles: true, cancelable: true }));
        }
        _touchDragSource.element.dispatchEvent(new Event('dragend', { bubbles: true }));
    }
    document.removeEventListener('touchmove', _onTouchDragMove, { passive: false });
    document.removeEventListener('touchend', _onTouchDragEnd);
    document.removeEventListener('touchcancel', _onTouchDragEnd);
    _touchDragSource = null;
};

document.addEventListener('touchstart', (e) => {
    if (e.touches.length !== 1) return;
    const t = e.touches[0];
    const target = (t.target instanceof Element) ? t.target : null;
    if (!target) return;
    const src = target.closest('[draggable="true"]');
    if (!src) return;
    if (src.closest('[data-no-touch-drag="true"]')) return;

    _touchDragSource = {
        element: src,
        startX: t.clientX,
        startY: t.clientY,
        started: false,
        lastOver: null,
    };
    // touchmove must be non-passive so we can preventDefault once the drag starts.
    document.addEventListener('touchmove', _onTouchDragMove, { passive: false });
    document.addEventListener('touchend', _onTouchDragEnd, { passive: true });
    document.addEventListener('touchcancel', _onTouchDragEnd, { passive: true });
}, { passive: true });

export function registerClickOutside(elementId, triggerElementId, dotnetRef) {
    clickOutsideHandlers.set(elementId, { triggerElementId, dotnetRef });
}

export function unregisterClickOutside(elementId) {
    clickOutsideHandlers.delete(elementId);
}

export function focusElement(element) {
    if (element) {
        element.focus();
    }
}

export function focusElementById(id) {
    const el = document.getElementById(id);
    if (el) {
        el.focus();
    }
}

let scrollLockCount = 0;

// Setting overflow:hidden on <body> alone is insufficient on iOS Safari and
// some Firefox configurations — the page still scrolls because the scroll
// chain reaches <html>. We lock both elements together (and restore both on
// unlock) so the modal/sheet/overlay actually traps scroll across browsers.
let lockedPaddingRight = '';

export function lockScroll() {
    scrollLockCount++;
    if (scrollLockCount === 1) {
        // Compensate for the scrollbar that overflow:hidden removes. Without this the
        // page content (and any position:fixed chrome) jumps right by the scrollbar
        // width the instant an overlay opens — the classic modal layout-shift jank.
        // Measure the gutter BEFORE hiding overflow, then pad the body by it.
        const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
        lockedPaddingRight = document.body.style.paddingRight;
        if (scrollbarWidth > 0) {
            const current = parseFloat(getComputedStyle(document.body).paddingRight) || 0;
            document.body.style.paddingRight = `${current + scrollbarWidth}px`;
        }
        document.body.style.overflow = 'hidden';
        document.documentElement.style.overflow = 'hidden';
    }
}

export function unlockScroll() {
    scrollLockCount = Math.max(0, scrollLockCount - 1);
    if (scrollLockCount === 0) {
        document.body.style.overflow = '';
        document.documentElement.style.overflow = '';
        // Restore the exact inline padding-right we saved (empty string → falls back
        // to the stylesheet value), undoing the scrollbar-width compensation.
        document.body.style.paddingRight = lockedPaddingRight;
        lockedPaddingRight = '';
    }
}

// Toggles a class on <html>. Used by DataGrid fullscreen to signal consumers
// (e.g. a docs navbar) that they should hide floating chrome.
export function setHtmlClass(className, active) {
    if (!className) return;
    document.documentElement.classList.toggle(className, !!active);
}

const focusTrapHandlers = new Map();

const FOCUS_TRAP_FOCUSABLE =
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]):not([disabled])';

// initialFocusSelector (optional): a CSS selector resolved WITHIN the trapped
// element naming the preferred initial-focus target — e.g. AlertDialog passes
// [data-lumeo-initial-focus] so focus lands on the least destructive action
// (the Cancel button) instead of whatever is first in DOM order. Falls back
// to the first focusable element when absent or not found.
export function setupFocusTrap(elementId, initialFocusSelector) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // Remember what had focus before the trap engages (normally the trigger
    // button) so removeFocusTrap can hand focus back on close. Without this,
    // keyboard users are dropped at <body> after every overlay dismissal.
    const ae = document.activeElement;
    const previousFocus = (ae && ae !== document.body && typeof ae.focus === 'function') ? ae : null;

    const handler = (e) => {
        if (e.key !== 'Tab') return;
        const focusable = el.querySelectorAll(FOCUS_TRAP_FOCUSABLE);
        if (focusable.length === 0) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
            last.focus();
            e.preventDefault();
        } else if (!e.shiftKey && document.activeElement === last) {
            first.focus();
            e.preventDefault();
        }
    };

    focusTrapHandlers.set(elementId, { handler, previousFocus });
    el.addEventListener('keydown', handler);

    let initial = null;
    if (initialFocusSelector) {
        try { initial = el.querySelector(initialFocusSelector); } catch { initial = null; }
    }
    if (initial && typeof initial.focus === 'function') {
        initial.focus();
    } else if (el.tabIndex >= -1 && typeof el.focus === 'function') {
        // Focus the PANEL itself (all overlay shells carry tabindex="-1"), not the
        // first focusable child — Radix/vaul parity. Auto-focusing the first child
        // meant a form overlay focused its first <input> mid slide-in, summoning
        // the mobile keyboard while the panel was still animating (an iOS
        // visual-viewport + position:fixed hazard implicated in the stacked-drawer
        // breakage report, B4). Escape/Tab still work from the panel, and the trap
        // below keeps Tab cycling within it; a consumer who wants a specific
        // control focused passes initialFocusSelector (AlertDialog already does).
        el.focus();
    } else {
        const focusable = el.querySelectorAll(FOCUS_TRAP_FOCUSABLE);
        if (focusable.length > 0) {
            focusable[0].focus();
        }
    }
}

export function removeFocusTrap(elementId) {
    const entry = focusTrapHandlers.get(elementId);
    if (!entry) return;
    const el = document.getElementById(elementId);
    if (el) {
        el.removeEventListener('keydown', entry.handler);
    }
    focusTrapHandlers.delete(elementId);
    // Return focus to the element that was focused before the trap engaged
    // (WCAG 2.4.3). Runs even when the overlay element is already gone from
    // the DOM (dispose-while-open) — the trigger usually still exists. Guard
    // everything: the trigger itself may have been removed in the meantime,
    // and focus() can throw on detached/inert elements in some engines.
    const prev = entry.previousFocus;
    if (prev && prev.isConnected && typeof prev.focus === 'function') {
        try { prev.focus(); } catch { /* element no longer focusable — ignore */ }
    }
}

// --- Lightweight focus save / restore (no Tab trap) ---------------------------
// For NON-modal overlays — menus, listbox popovers — where focus moves into the
// surface on open but Tab must NOT be trapped (the WAI-ARIA menu pattern closes
// the menu on Tab). saveFocus stashes whatever had focus (the trigger) keyed by
// the surface id; restoreFocus hands it back on close (WCAG 2.4.3) without
// installing any keydown handler. Distinct map from setupFocusTrap so the two
// never collide.
const savedFocusByKey = new Map();
export function saveFocus(key) {
    const ae = document.activeElement;
    savedFocusByKey.set(key, (ae && ae !== document.body && typeof ae.focus === 'function') ? ae : null);
}
export function restoreFocus(key) {
    const prev = savedFocusByKey.get(key);
    savedFocusByKey.delete(key);
    if (!(prev && prev.isConnected && typeof prev.focus === 'function')) return;
    // Only hand focus back if the user hasn't ALREADY moved it elsewhere. Restore when focus was lost to
    // <body> (the dismissed surface took its focused child down with it) or still sits inside the dismissed
    // surface (`key` is the surface's content id) — e.g. an Escape/keyboard dismiss. If focus is on some
    // OTHER connected control, a controlled close (parent dismisses the surface while the user is typing in
    // another field) would otherwise STEAL focus back to the old trigger (Codex P2).
    const ae = document.activeElement;
    const surface = document.getElementById(key);
    if (ae && ae !== document.body && !(surface && surface.contains(ae))) return;
    try { prev.focus(); } catch { /* element no longer focusable — ignore */ }
}

// --- Slide-animation transform cleanup (Sheet / Drawer) ---
//
// Two bugs to defeat in this fix:
//
// (1) CSS Cascade Level 4 priority. Per spec, animations rank ABOVE normal
//     author declarations. The order from highest to lowest is:
//       Transitions
//       !important user-agent
//       !important user
//       !important author        ← inline style WITH !important
//       Animations               ← fill-mode forwarded values live here
//       Normal author            ← inline style="..." WITHOUT !important
//       Normal user
//       Normal user agent
//     So plain `el.style.transform = 'none'` can't override the
//     fill-mode-forwarded `to: translateX(0)`. We need `setProperty(..., 'important')`.
//
// (2) Race between listener-attach and animation-end. This function is
//     called from C# OnAfterRenderAsync via JS interop. Even in WASM mode
//     that introduces enough latency (microtasks + frame boundaries) that
//     the slide animation (300 ms) can already be `finished` by the time
//     `addEventListener('animationend', ...)` runs. addEventListener does
//     NOT replay events that fired before attach, so the listener never
//     sees the end event and the cleanup never runs. Empirically verified.
//
// The fix uses the Web Animations API instead of an event listener:
// `el.getAnimations()` returns running OR already-finished animations
// (as long as fill-mode keeps them alive), and `animation.finished` is a
// Promise that resolves immediately if the animation already finished,
// or once it does. Race-condition-free regardless of when this runs.
//
// History of placebo fixes that taught me this the hard way:
// - rc.21: keyframes ending in `to: none` with a 99% intermediate.
//   Chrome interpolates none ↔ translateX(0) as identity matrix anyway
//   under fill-mode:both. End state stuck at matrix(1,0,0,1,0,0).
// - rc.22: @onanimationend Razor handler dropping the class. Blazor's
//   EventArgs doesn't expose animationName so descendant animations
//   bubbled up and prematurely dropped the class mid-slide.
// - rc.23: native JS listener with `el.style.transform = 'none'`.
//   Filtered correctly, but plain inline is rank 6 — animations win.
// - rc.24: same listener with `!important`. Cascade priority correct,
//   but the listener attaches AFTER animation ends, so animationend
//   never fires for it. Cleanup never runs.
// - rc.25 (this): getAnimations() + .finished + setProperty important.
//   Race-free AND cascade-correct. Verified by user reproducing in
//   Chrome 131 with the exact pattern below.
export async function attachOverlaySlideEnd(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // Fresh open — clear any exit latch left over from a previous close that was
    // interrupted by a re-open (rapid close/reopen). The latch below makes this
    // enter-helper skip its `animation:none` stamp while an exit is in flight, so
    // it must be reset on every open or a reopened overlay would never regain the
    // containing-block guard.
    el.removeAttribute('data-lumeo-exit');

    // Web Animations API: returns running + already-finished animations
    // (fill-mode keeps finished ones alive in this list).
    // 'zoom-in' is included alongside the sheet/drawer slide-ins: the Dialog/
    // AlertDialog panel's animate-zoom-in ALSO transform-animates with
    // fill-mode:both, so the settled panel keeps an identity-matrix transform
    // (matrix(1,0,0,1,0,0)) forever — a permanent containing block for every
    // position:fixed descendant (the B1 "Select inside a dialog lands offset"
    // report's second ingredient; the lumeo.css keyframe comment claiming the
    // `to: transform:none` avoids this is factually wrong under fill-mode:both,
    // exactly the rc.21 lesson above).
    const slideAnimations = el.getAnimations({ subtree: false })
        .filter(a => typeof a.animationName === 'string'
                  && (a.animationName.startsWith('slide-in-from-')
                      || a.animationName === 'zoom-in'));

    if (slideAnimations.length === 0) {
        // No slide animation found — defensive: clear both transform AND
        // animation (in case a partial state somehow remains). Skip if an exit
        // has already begun (see the data-lumeo-exit guard below).
        if (!el.getAttribute('data-lumeo-exit')) {
            el.style.setProperty('transform', 'none', 'important');
            el.style.setProperty('animation', 'none', 'important');
        }
        return;
    }

    await Promise.all(slideAnimations.map(async (anim) => {
        try { await anim.finished; }
        catch { /* playState 'cancelled' or 'idle' — ignore */ }
    }));

    // Rapid close/reopen race: if the close landed WHILE we were awaiting the
    // enter animation above, attachOverlayExitEnd has already cleared the inline
    // guard and started the slide/zoom-OUT keyframe. Stamping animation:none now
    // would freeze that exit mid-flight (the pre-fix B11 shape, in reverse). The
    // exit helper set data-lumeo-exit before we resumed — bail so it owns the
    // element's animation from here.
    if (el.getAttribute('data-lumeo-exit')) return;

    // CRITICAL: clear BOTH transform AND animation. Empirically verified
    // (Chrome 131): clearing only transform is insufficient. Even with
    // computed transform = 'none' (overridden via !important), the active
    // 'animation' declaration makes Chrome establish a containing block
    // for position:fixed descendants — Chrome's compositor pre-creates a
    // layer for any element with a transform-animating animation,
    // regardless of the current computed transform value.
    //
    // Smoking-gun test: with sheet at transform:none + animation declared,
    // a position:fixed descendant popover renders at sheet.x + style.left
    // (sheet acts as containing block). Set animation:none and the
    // popover snaps back to viewport-relative positioning.
    el.style.setProperty('animation', 'none', 'important');
    el.style.setProperty('transform', 'none', 'important');
}

// Drives the overlay EXIT the way Radix's Presence does: keep the panel mounted,
// let its own exit keyframe run to completion, and unmount on the real animation
// end rather than a blind timer. Two jobs:
//   1. Remove the open-time containing-block guard (the inline `animation:none` /
//      `transform:none` that attachOverlaySlideEnd stamped once the enter animation
//      settled). While that inline `!important` is present it OVERRIDES the
//      `animate-slide-out-*` / `animate-zoom-out` class the component just applied,
//      so the panel cannot animate out at all — it sits frozen until a timer yanks
//      it, while only the backdrop (never stamped) fades. That is the exact B11
//      "backdrop animates but the panel doesn't move with it" report, and it is
//      structural, not a timing race — no duration/latch tweak can fix it.
//   2. Await the panel's exit animation .finished, then notify .NET ONCE so the
//      component drops backdrop + panel together in the same render commit.
// data-lumeo-exit latches the exit so a still-pending attachOverlaySlideEnd (rapid
// close during the enter animation) won't re-stamp animation:none and freeze us.
export async function attachOverlayExitEnd(elementId, dotnetRef) {
    const notify = () => { try { dotnetRef?.invokeMethodAsync('OnExitAnimationEnd'); } catch { /* circuit gone */ } };
    const el = document.getElementById(elementId);
    if (!el) { notify(); return; }

    el.setAttribute('data-lumeo-exit', '1');
    // Drop the inline guard so the exit keyframe governs. removeProperty is a
    // no-op when nothing was stamped (Fade/None, or a close before slide-in
    // settled) — harmless.
    el.style.removeProperty('animation');
    el.style.removeProperty('transform');

    // Collect the exit animations the class now applies on THIS element (not
    // descendants — a Calendar zoom-in etc. inside the panel must not gate us).
    // getAnimations() forces a style flush, so the freshly-applicable slide/zoom/
    // fade-out shows up here immediately after the inline override was removed.
    const exit = el.getAnimations({ subtree: false })
        .filter(a => typeof a.animationName === 'string'
                  && (a.animationName.startsWith('slide-out-to-')
                      || a.animationName === 'zoom-out'
                      || a.animationName === 'fade-out'));

    if (exit.length === 0) {
        // No exit animation (Animation=None, or reduced-motion already elapsed the
        // 1ms keyframe) — unmount immediately, both elements together.
        notify();
        return;
    }

    await Promise.all(exit.map(async (anim) => {
        try { await anim.finished; }
        catch { /* cancelled by a re-open — the reopen path will re-render */ }
    }));
    notify();
}

// --- Floating Position ---

const positionCleanups = new Map();

export function positionFixed(contentId, referenceId, align, matchWidth, side, offset, dotnetRef) {
    const content = document.getElementById(contentId);
    const reference = document.getElementById(referenceId);
    if (!content || !reference) return side || 'bottom';

    const resolvedSide = side || 'bottom';
    // The side the box ACTUALLY lands on after any collision flip — returned to the caller so a
    // directional-arrow consumer (Tooltip) can keep its arrow on the edge facing the trigger.
    let computedSide = resolvedSide;
    // round-14: the synchronous return above only covers the INITIAL placement. A later scroll/resize
    // reposition (the rAF-driven update() below) can flip the box again while the surface stays open, and
    // a directional-arrow consumer needs to follow THAT too — so report any LATER change to dotnetRef.
    // lastReportedSide starts null so the first update() pass (the synchronous one) never notifies — that
    // placement is already conveyed by the function's return value.
    let lastReportedSide = null;
    // Trigger->content gap. Callers that don't pass an offset (legacy 5-arg
    // interop path) keep the historical 4px default.
    const gap = (typeof offset === 'number' && Number.isFinite(offset) && offset >= 0) ? offset : 4;

    // Clean up any previous listener for this content
    if (positionCleanups.has(contentId)) {
        positionCleanups.get(contentId)();
    }

    function update() {
        if (!content.isConnected || !reference.isConnected) {
            cleanup();
            return;
        }

        const refRect = reference.getBoundingClientRect();

        content.style.position = 'fixed';
        content.style.zIndex = '50';

        if (matchWidth) {
            content.style.width = `${refRect.width}px`;
        }

        // --- Transform-free positioning (#172) ---------------------------------
        // Position with explicit top/left ONLY; never set a CSS `transform` on
        // overlay content. A transformed ancestor establishes a containing block
        // for its `position:fixed` descendants — which is exactly why a nested
        // overlay (DropdownMenuSubContent, popover-in-popover, ContextMenu/Menubar
        // sub-content) positioned via this same function resolved against the
        // transformed parent instead of the viewport and rendered off-screen.
        // Folding the former translateX/Y(-50%/-100%) offsets into the computed
        // coordinates keeps the visual result identical while leaving the overlay
        // transform-free, so nested fixed overlays resolve against the viewport.
        content.style.transform = '';

        // Reset any prior clamp so we measure the natural content size first
        // (otherwise a previous tight maxHeight would skew the measurement).
        content.style.maxHeight = '';
        content.style.overflow = '';

        // Measure the natural box. matchWidth was already applied above, so width
        // (and therefore wrap-dependent height) is final. offsetWidth/Height force
        // a synchronous layout flush — without it an in-flight animation scale or
        // an uncommitted Blazor render can report stale dimensions (empirically
        // cr.height = 1574 when the settled height was 310).
        void content.offsetHeight;
        let cw = content.offsetWidth;
        let ch = content.offsetHeight;

        // Compute explicit top/left for the requested side + align. The size
        // shifts replace the former transforms: translateX(-50%) -> -cw/2,
        // translateX(-100%) -> -cw, translateY(-50%) -> -ch/2,
        // translateY(-100%) -> -ch.
        // "align" is a LOGICAL (writing-mode-relative) direction only on the HORIZONTAL
        // axis — i.e. for side === 'top'/'bottom', where it picks the box's left/right
        // edge. DirectionProvider makes start/end logical for the component tree, but
        // this entry point had no direction input, so align="start" always resolved to
        // the physical LEFT edge even under <DirectionProvider Direction="Rtl"> — where
        // "start" should mean the trigger's RIGHT edge (Select/DropdownMenu pass "start"
        // and landed flipped). The side === 'left'/'right' usage below (picking the
        // box's top/bottom edge) is a BLOCK-direction concern that RTL does not flip, so
        // it is deliberately left untouched.
        const isRtl = getComputedStyle(reference).direction === 'rtl';
        const horizontalAlign = isRtl
            ? (align === 'center' ? 'center' : (align === 'end' ? 'start' : 'end'))
            : align;

        let top, left;
        if (resolvedSide === 'top') {
            top = refRect.top - gap - ch;
            if (horizontalAlign === 'center') left = refRect.left + refRect.width / 2 - cw / 2;
            else if (horizontalAlign === 'end') left = refRect.right - cw;
            else left = refRect.left;
        } else if (resolvedSide === 'left') {
            left = refRect.left - gap - cw;
            if (align === 'center') top = refRect.top + refRect.height / 2 - ch / 2;
            else if (align === 'end') top = refRect.bottom - ch;
            else top = refRect.top;
        } else if (resolvedSide === 'right') {
            left = refRect.right + gap;
            if (align === 'center') top = refRect.top + refRect.height / 2 - ch / 2;
            else if (align === 'end') top = refRect.bottom - ch;
            else top = refRect.top;
        } else {
            // bottom (default)
            top = refRect.bottom + gap;
            if (horizontalAlign === 'center') left = refRect.left + refRect.width / 2 - cw / 2;
            else if (horizontalAlign === 'end') left = refRect.right - cw;
            else left = refRect.left;
        }

        // Apply, then re-measure for the flip/clamp guards below.
        content.style.top = `${top}px`;
        content.style.left = `${left}px`;
        content.style.right = '';

        // --- Containing-block compensation (transformed ancestor) -------------
        // position:fixed resolves against the nearest transformed / filtered /
        // backdrop-filtered ancestor (which becomes its containing block), NOT
        // the viewport. When such an ancestor exists — e.g. this content is a
        // SelectContent / DropdownMenuSubContent rendered inside a centered
        // Dialog (which uses transform) — every top/left we write lands offset
        // by the ancestor's origin, so the popover renders off its trigger. All
        // the math in this function is in viewport space, so a freshly-written
        // content.style.top/left holds an INTENDED viewport coordinate; measuring
        // the residual between intended and actual yields exactly the ancestor's
        // origin offset, which we fold back. Applied here — BEFORE the flip/
        // clamp overflow checks below — so those checks measure the box's TRUE
        // on-screen position; measuring the uncompensated (ancestor-offset-
        // inflated) position could make a box that actually fits on screen look
        // like it overflows, triggering an unwarranted flip. Applied AGAIN at
        // the end of this function (after the flip/clamp logic may have written
        // fresh top/left values of its own, which need the same fold-back). A
        // no-op (offset ~ 0) when no transformed ancestor exists, so it never
        // affects the common page-level case. Compensating here instead of
        // reparenting the node to <body> keeps Blazor's DOM ownership intact.
        //
        // IDEMPOTENCE (production bug, reported as "Select inside a service
        // dialog lands offset"): folding must happen exactly ONCE per axis per
        // freshly-written viewport value. The old code re-read style.top/left on
        // the second call and treated the ALREADY-FOLDED value as a new intent —
        // measuring the (now correct) settled position against it re-derived the
        // ancestor offset and subtracted it AGAIN, landing the box at exactly
        // `intended − ancestorOrigin` whenever the flip/clamp branches did NOT
        // rewrite that axis (the common no-flip case; empirically proven with a
        // Chromium harness against the shipped 4.0.4 assets).
        //
        // IDEMPOTENCE is keyed on the SERIALIZED value we last wrote, NOT the
        // parsed float. The prior implementation compared `parseFloat(style.left)
        // !== foldedLeft` (a number stored in JS) — but the CSSOM can re-serialize
        // a written length at a slightly different precision, so reading the value
        // back parsed to a number that no longer `===` the in-memory `foldedLeft`.
        // The guard then treated the ALREADY-FOLDED value as a fresh viewport
        // intent and folded it a SECOND time within the same update(), landing the
        // box at exactly `intended − ancestorOrigin`. That surfaced as a popover
        // opening one containing-block-offset to the LEFT of its trigger whenever
        // it lived inside a `will-change: transform` / transformed / filtered
        // ancestor (e.g. a blur-fade-wrapped page region). Comparing the
        // browser-normalised string of what we last wrote is exact, so an axis is
        // re-folded ONLY when the flip/clamp logic below actually replaced it with
        // a new viewport-space value (which needs the same fold-back).
        let foldedTopStr = null, foldedLeftStr = null;
        function compensateContainingBlock() {
            void content.offsetHeight;
            const settled = content.getBoundingClientRect();
            if (content.style.top !== foldedTopStr) {
                const curTop = parseFloat(content.style.top);
                if (Number.isFinite(curTop)) {
                    const offY = settled.top - curTop;
                    const folded = Math.abs(offY) > 0.5 ? curTop - offY : curTop;
                    if (folded !== curTop) content.style.top = `${folded}px`;
                    foldedTopStr = content.style.top;
                }
            }
            if (content.style.left !== foldedLeftStr) {
                const curLeft = parseFloat(content.style.left);
                if (Number.isFinite(curLeft)) {
                    const offX = settled.left - curLeft;
                    const folded = Math.abs(offX) > 0.5 ? curLeft - offX : curLeft;
                    if (folded !== curLeft) content.style.left = `${folded}px`;
                    foldedLeftStr = content.style.left;
                }
            }
        }
        compensateContainingBlock();

        void content.offsetHeight;
        const cr = content.getBoundingClientRect();

        // Flip vertical if overflows bottom — but ONLY if flipping up actually
        // gives us more usable space. Without this guard, a popover that's
        // taller than the trigger's `top` (e.g. a calendar grown to fill its
        // parent flex container — observed at ~1574px inside a Sheet) ends
        // up with `top: triggerTop - contentHeight - gap` going far negative,
        // rendering off-screen at the top. Clamp to viewport with maxHeight.
        // (Transform-free: recompute `top` directly instead of stripping a
        // translateY from a transform string.)
        if (resolvedSide === 'bottom' && cr.bottom > window.innerHeight) {
            const newRefRect = reference.getBoundingClientRect();
            const spaceAbove = newRefRect.top - 8;        // 8px breathing room
            const spaceBelow = window.innerHeight - newRefRect.bottom - 8;
            if (spaceAbove >= cr.height + gap) {
                // Flip up — fits naturally
                content.style.top = `${newRefRect.top - cr.height - gap}px`;
            } else if (spaceAbove > spaceBelow) {
                // More room above than below — flip up and cap height
                content.style.top = `8px`;
                content.style.maxHeight = `${spaceAbove - gap}px`;
                content.style.overflow = 'auto';
            } else {
                // Stick below the trigger and cap height to viewport
                content.style.top = `${newRefRect.bottom + gap}px`;
                content.style.maxHeight = `${spaceBelow}px`;
                content.style.overflow = 'auto';
            }
        }
        // Flip vertical if overflows top — same guard logic
        if (resolvedSide === 'top' && cr.top < 0) {
            const newRefRect = reference.getBoundingClientRect();
            const spaceAbove = newRefRect.top - 8;
            const spaceBelow = window.innerHeight - newRefRect.bottom - 8;
            if (spaceBelow >= cr.height + gap) {
                content.style.top = `${newRefRect.bottom + gap}px`;
            } else if (spaceBelow > spaceAbove) {
                content.style.top = `${newRefRect.bottom + gap}px`;
                content.style.maxHeight = `${spaceBelow}px`;
                content.style.overflow = 'auto';
            } else {
                content.style.top = `8px`;
                content.style.maxHeight = `${spaceAbove - gap}px`;
                content.style.overflow = 'auto';
            }
        }
        // Clamp horizontal
        if (cr.right > window.innerWidth) {
            content.style.left = `${Math.max(8, window.innerWidth - cr.width - 8)}px`;
        }
        if (cr.left < 0) {
            content.style.left = '8px';
        }

        // Universal final guard: ensure the popover (a) never exceeds the
        // viewport height and (b) never has a negative top. Belt-and-
        // suspenders for the user-reported rc.21–rc.23 `top: -1156` —
        // even if upstream logic somehow miscalculates (e.g. layout
        // returning huge cr.height during an in-flight render), the
        // popover is forced into the viewport with a scrollable cap.
        const finalCr = content.getBoundingClientRect();
        const maxAllowedHeight = window.innerHeight - 16;
        if (finalCr.height > maxAllowedHeight) {
            content.style.maxHeight = `${maxAllowedHeight}px`;
            content.style.overflow = 'auto';
        }
        const finalTop = parseFloat(content.style.top);
        if (Number.isFinite(finalTop) && finalTop < 0) {
            content.style.top = '8px';
            content.style.maxHeight = `${maxAllowedHeight}px`;
            content.style.overflow = 'auto';
        }
        // Also clamp if the popover extends past the viewport bottom (could
        // happen if our flip-up branch wasn't reached for some side variant).
        const updatedCr = content.getBoundingClientRect();
        if (updatedCr.bottom > window.innerHeight - 8) {
            const clampedTop = Math.max(8, window.innerHeight - 8 - updatedCr.height);
            content.style.top = `${clampedTop}px`;
            if (updatedCr.height > maxAllowedHeight) {
                content.style.maxHeight = `${maxAllowedHeight}px`;
                content.style.overflow = 'auto';
            }
        }

        // Fold back the containing-block offset a second time: the flip/clamp
        // logic above may have written fresh top/left values of its own (e.g. a
        // vertical flip, or a viewport-edge clamp), and those are ALSO
        // viewport-space intended values that need the same ancestor-origin
        // compensation applied near the top of this function.
        compensateContainingBlock();

        // Report the side the box ACTUALLY landed on. A collision flip above may have moved a preferred
        // Top box below its trigger (or vice-versa); compare final box-center vs reference-center so a
        // directional-arrow consumer can re-point its arrow to the edge facing the trigger.
        const rRect = reference.getBoundingClientRect();
        const cRect = content.getBoundingClientRect();
        if (resolvedSide === 'top' || resolvedSide === 'bottom') {
            computedSide = (cRect.top + cRect.height / 2) <= (rRect.top + rRect.height / 2) ? 'top' : 'bottom';
        } else if (resolvedSide === 'left' || resolvedSide === 'right') {
            computedSide = (cRect.left + cRect.width / 2) <= (rRect.left + rRect.width / 2) ? 'left' : 'right';
        }

        // Arrow anchor (floating-ui "arrow middleware" equivalent): a directional-arrow
        // consumer (Tooltip) renders its arrow at `var(--lumeo-arrow-x/y, 50%)` — the 50%
        // fallback is the classic box-centered arrow. When the viewport-edge clamps above
        // shift the box away from being trigger-centered (e.g. a trigger near the screen
        // edge), a box-centered arrow points into empty space instead of at the trigger;
        // compute where the TRIGGER's center actually falls within the final box and pin
        // the arrow there. Clamped to 12px from either box edge so the rotated square
        // never escapes the box's rounded corners. Set unconditionally — consumers that
        // don't read the vars are unaffected.
        const arrowX = Math.min(Math.max(rRect.left + rRect.width / 2 - cRect.left, 12), Math.max(cRect.width - 12, 12));
        const arrowY = Math.min(Math.max(rRect.top + rRect.height / 2 - cRect.top, 12), Math.max(cRect.height - 12, 12));
        content.style.setProperty('--lumeo-arrow-x', `${arrowX}px`);
        content.style.setProperty('--lumeo-arrow-y', `${arrowY}px`);

        // Notify .NET of a LATER side change (skips the very first pass — lastReportedSide is still null
        // there, and that placement is already conveyed by positionFixed's synchronous return).
        if (dotnetRef && lastReportedSide !== null && computedSide !== lastReportedSide) {
            dotnetRef.invokeMethodAsync('OnPositionSideChanged', contentId, computedSide).catch(() => {});
        }
        lastReportedSide = computedSide;
    }

    // Initial position
    update();

    // Reposition on scroll/resize so popup follows the trigger
    let rafId = 0;
    const onScrollOrResize = () => {
        cancelAnimationFrame(rafId);
        rafId = requestAnimationFrame(update);
    };

    window.addEventListener('scroll', onScrollOrResize, { capture: true, passive: true });
    window.addEventListener('resize', onScrollOrResize, { passive: true });

    // Reference-rect watchdog (floating-ui autoUpdate({animationFrame:true})
    // semantics): a trigger that MOVES without any scroll/resize — e.g. the
    // sidebar-toggle button riding the sidebar's animating width, a layout
    // shift from content inserted above, an accordion expanding — fires
    // neither listener above, so the fixed-position box froze at its opening
    // coordinates (user-reported). Poll the reference's rect once per frame
    // and re-run the full pipeline only when it actually changed: the idle
    // cost is a single getBoundingClientRect() read with zero writes
    // (sub-0.05ms, no layout thrash — layout is clean between frames), the
    // expensive update() runs only on frames where the trigger really moved.
    // Exact !== compare (floating-ui does the same): during an animation every
    // subpixel change is real movement; at rest the rect is bit-stable.
    // The scroll/resize listeners stay — a window resize can require a
    // re-clamp even when the reference keeps its viewport coordinates.
    let lastRefRect = reference.getBoundingClientRect();
    let watchId = requestAnimationFrame(function watch() {
        if (!reference.isConnected || !content.isConnected) { cleanup(); return; }
        const r = reference.getBoundingClientRect();
        if (r.top !== lastRefRect.top || r.left !== lastRefRect.left ||
            r.width !== lastRefRect.width || r.height !== lastRefRect.height) {
            lastRefRect = r;
            update();
        }
        watchId = requestAnimationFrame(watch);
    });

    // Content-resize re-clamp: a popover that GROWS after opening (async
    // Items landing in a Select, search filter expanding a list) never
    // re-enters update(), so the viewport-overflow clamp/maxHeight logic ran
    // only against the small initial box and a long list ended up taller than
    // the screen with no scroll (user-reported). ResizeObserver on the content
    // drives the same rAF-debounced update() as scroll/resize. Guarded — RO is
    // universal in supported browsers but keeps test hosts (bUnit/jsdom) safe.
    let contentRO = null;
    if (typeof ResizeObserver === 'function') {
        let firstRO = true;
        contentRO = new ResizeObserver(() => {
            // The observer fires once immediately on observe() — that's the
            // just-measured initial size, already handled by update() above.
            if (firstRO) { firstRO = false; return; }
            onScrollOrResize();
        });
        contentRO.observe(content);
    }

    const cleanup = () => {
        cancelAnimationFrame(rafId);
        cancelAnimationFrame(watchId);
        if (contentRO) { try { contentRO.disconnect(); } catch (_) {} contentRO = null; }
        // Wrap removeEventListener in try/catch — the window object can
        // throw in detached / cross-realm scenarios (worker hosts, MAUI
        // Hybrid teardown), and we don't want a failure to remove one
        // listener to skip the rest of the cleanup or the
        // positionCleanups.delete below.
        try { window.removeEventListener('scroll', onScrollOrResize, { capture: true }); } catch (_) {}
        try { window.removeEventListener('resize', onScrollOrResize); } catch (_) {}
        positionCleanups.delete(contentId);
    };
    positionCleanups.set(contentId, cleanup);
    return computedSide;
}

export function unpositionFixed(contentId) {
    const fn = positionCleanups.get(contentId);
    if (!fn) return;
    // Even though cleanup() itself is now defensive, a stray exception
    // mustn't leave the cleanup half-done — guard the call site too so
    // a future change to the cleanup body can't take the registry into
    // an inconsistent state.
    try { fn(); } catch (_) { positionCleanups.delete(contentId); }
}

// Position a fixed element so its top-left starts at the point (x, y) — used by
// ContextMenu, which opens at raw click coordinates with no anchor element so
// positionFixed (reference-element based) doesn't apply. Clamps the element
// into the viewport: if it would overflow the right/bottom edge it flips to
// open up/left of the point (native context-menu behaviour), and as a final
// guard keeps it >= 8px from every edge. Returns nothing; safe if missing.
export function positionAtPoint(contentId, x, y) {
    const el = document.getElementById(contentId);
    if (!el) return;

    const margin = 8;
    el.style.position = 'fixed';
    el.style.transform = '';
    // Place at the raw point first, then measure the natural size.
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
    void el.offsetHeight; // force layout flush before measuring
    const rect = el.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    let left = x;
    let top = y;

    // Horizontal: flip to the left of the cursor if it overflows the right edge,
    // then clamp so it never sits past either edge.
    if (left + rect.width > vw - margin) {
        left = x - rect.width;
    }
    left = Math.max(margin, Math.min(left, vw - rect.width - margin));

    // Vertical: flip above the cursor if it overflows the bottom edge, then clamp.
    if (top + rect.height > vh - margin) {
        top = y - rect.height;
    }
    top = Math.max(margin, Math.min(top, vh - rect.height - margin));

    el.style.left = `${left}px`;
    el.style.top = `${top}px`;

    // Containing-block compensation — same fold-back as positionFixed's
    // compensateContainingBlock (see there for the full rationale): a transformed/
    // filtered ancestor (e.g. a service dialog's animated panel) becomes this
    // fixed element's containing block, so the viewport-space left/top above land
    // offset by the ancestor's origin. Measure the residual once and subtract it.
    // positionAtPoint previously had NO compensation at all, so a ContextMenu
    // opened inside a dialog rendered at click-point + panel-origin (empirically
    // confirmed against the 4.0.4 assets). Single write → single fold; the
    // idempotence bookkeeping positionFixed needs does not apply here.
    void el.offsetHeight;
    const settled = el.getBoundingClientRect();
    const offX = settled.left - left;
    const offY = settled.top - top;
    if (Math.abs(offX) > 0.5) el.style.left = `${left - offX}px`;
    if (Math.abs(offY) > 0.5) el.style.top = `${top - offY}px`;
}

// --- Viewport Size ---

export function getViewportSize() {
    return { width: window.innerWidth, height: window.innerHeight };
}

// --- Element Rect ---

export function getElementRect(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return null;
    const rect = el.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
}

export function getElementDimension(elementId, dimension) {
    const el = document.getElementById(elementId);
    if (!el) return 0;
    const rect = el.getBoundingClientRect();
    return dimension === 'width' ? rect.width : rect.height;
}

// --- Scroll position lookup (used by PullToRefresh to gate pointer-down) ---
// Returns the element's own scrollTop. Consumers that want the window's
// document scroll can pass the special id "__window__".
export function getScrollTop(elementId) {
    if (elementId === '__window__') {
        return window.scrollY || document.documentElement.scrollTop || 0;
    }
    const el = document.getElementById(elementId);
    if (!el) return 0;
    return el.scrollTop || 0;
}

// --- Pull-to-refresh gesture guard (#308) ---
//
// PullToRefresh's wrapper IS the scroll container. With CSS `touch-action:
// pan-y` the browser owns vertical panning, so a downward drag at scrollTop 0
// is consumed as native (over)scroll and the Blazor pointermove deltas that
// drive the rubber-band never get a chance — the gesture is "stolen". A
// non-passive touchmove listener that calls preventDefault() ONLY while the
// container is at the top AND the finger is moving down hands that case to us,
// while leaving normal upward/inner scrolling fully native. This is the piece
// CSS alone can't express (touch-action can't say "only intercept downward at
// the top"). Pointer Events stay the source of truth for the visual offset.
const pullToRefreshHandlers = new Map();

export function registerPullToRefresh(elementId) {
    unregisterPullToRefresh(elementId);
    const el = document.getElementById(elementId);
    if (!el) return;

    let startY = 0;
    let tracking = false;

    const onTouchStart = (e) => {
        if (!e.touches || e.touches.length !== 1) { tracking = false; return; }
        // Only arm when already at the very top — otherwise this is a normal
        // inner scroll and must stay native.
        tracking = (el.scrollTop || 0) <= 0;
        startY = e.touches[0].clientY;
    };

    const onTouchMove = (e) => {
        if (!tracking || !e.touches || e.touches.length !== 1) return;
        const dy = e.touches[0].clientY - startY;
        // Downward pull while pinned at the top → claim it so the browser
        // doesn't overscroll/native-refresh. Upward (dy <= 0) stays native so
        // the user can scroll into content.
        if (dy > 0 && (el.scrollTop || 0) <= 0) {
            if (e.cancelable) e.preventDefault();
        } else {
            tracking = false;
        }
    };

    const onTouchEnd = () => { tracking = false; };

    const handlers = { onTouchStart, onTouchMove, onTouchEnd };
    el.addEventListener('touchstart', onTouchStart, { passive: true });
    // Must be non-passive so preventDefault is honored.
    el.addEventListener('touchmove', onTouchMove, { passive: false });
    el.addEventListener('touchend', onTouchEnd, { passive: true });
    el.addEventListener('touchcancel', onTouchEnd, { passive: true });
    pullToRefreshHandlers.set(elementId, { el, handlers });
}

export function unregisterPullToRefresh(elementId) {
    const entry = pullToRefreshHandlers.get(elementId);
    if (!entry) return;
    const { el, handlers } = entry;
    try {
        el.removeEventListener('touchstart', handlers.onTouchStart);
        el.removeEventListener('touchmove', handlers.onTouchMove);
        el.removeEventListener('touchend', handlers.onTouchEnd);
        el.removeEventListener('touchcancel', handlers.onTouchEnd);
    } catch (_) { /* noop */ }
    pullToRefreshHandlers.delete(elementId);
}

// --- Wheel picker helpers (DateWheelPicker / TimeWheelPicker) ---
// Accept a live ElementReference (no id round-trip) — Blazor passes the element
// directly. We just read scrollTop / write it back. The picker handles the rest.
export function wheelScrollTop(element) {
    return element ? (element.scrollTop || 0) : 0;
}

export function wheelScrollTo(element, top) {
    if (!element) return;
    element.scrollTop = top;
}

// --- Pointer Capture (used by Splitter dividers) ---

export function setPointerCaptureOnElement(elementId, pointerId) {
    const el = document.getElementById(elementId);
    if (!el) return;
    try { el.setPointerCapture(pointerId); } catch (_) { /* noop */ }
}

export function releasePointerCaptureOnElement(elementId, pointerId) {
    const el = document.getElementById(elementId);
    if (!el) return;
    try {
        if (el.hasPointerCapture && el.hasPointerCapture(pointerId)) {
            el.releasePointerCapture(pointerId);
        }
    } catch (_) { /* noop */ }
}

// --- Pinch Zoom (multi-touch via Pointer Events) ---
//
// Generic two-finger pinch-zoom detector. Pointer Events express multi-touch
// as multiple simultaneous PointerEvents with different `pointerId`s, so we
// track active pointers in a Map keyed by id. When exactly two pointers are
// down, we compute the Euclidean distance between them on every move and
// emit `scaleDelta = currentDistance / lastDistance` to .NET — a value
// slightly above or below 1.0 that the caller can multiply into its own
// accumulated zoom level.
//
// `touch-action: none` on the target is required: without it the browser
// claims the gesture for native page zoom/scroll before our move handlers
// see anything (this is the inverse of the intuitive `touch-action: pinch-
// zoom`, which would let the browser handle pinch ITSELF — the opposite of
// what we want here).
//
// `setPointerCapture` on pointerdown is what lets a finger drift outside the
// element's bounds without breaking the gesture: subsequent events for that
// pointerId stay routed to the capturing element until release.

const pinchZoomHandlers = new Map();

export function registerPinchZoom(elementId, dotnetRef, methodName) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // Clean up any previous registration on the same element.
    const prev = pinchZoomHandlers.get(elementId);
    if (prev) {
        el.removeEventListener('pointerdown', prev.onDown);
        el.removeEventListener('pointermove', prev.onMove);
        el.removeEventListener('pointerup', prev.onUp);
        el.removeEventListener('pointercancel', prev.onUp);
    }

    const pointers = new Map(); // pointerId -> { x, y }
    let lastDistance = null;
    const method = methodName || 'OnPinchZoom';

    const distance = () => {
        const pts = [...pointers.values()];
        if (pts.length !== 2) return null;
        const dx = pts[0].x - pts[1].x;
        const dy = pts[0].y - pts[1].y;
        return Math.sqrt(dx * dx + dy * dy);
    };

    const onDown = (e) => {
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        try { el.setPointerCapture(e.pointerId); } catch (_) { /* noop */ }
        if (pointers.size === 2) {
            lastDistance = distance();
        } else if (pointers.size > 2) {
            // Multi-touch beyond 2 fingers — invalidate baseline so we don't
            // resume pinch math against a stale 2-pointer distance.
            lastDistance = null;
        }
    };

    const onMove = (e) => {
        if (!pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size !== 2) return;
        const d = distance();
        if (lastDistance && d) {
            const scaleDelta = d / lastDistance;
            dotnetRef.invokeMethodAsync(method, elementId, scaleDelta).catch(() => {});
            lastDistance = d;
        }
    };

    const onUp = (e) => {
        pointers.delete(e.pointerId);
        try {
            if (el.hasPointerCapture && el.hasPointerCapture(e.pointerId)) {
                el.releasePointerCapture(e.pointerId);
            }
        } catch (_) { /* noop */ }
        if (pointers.size === 2) {
            // 3→2 transition: re-baseline against the current 2-pointer
            // distance so resumed pinch doesn't jump from a stale baseline.
            lastDistance = distance();
        } else if (pointers.size < 2) {
            lastDistance = null;
        }
    };

    el.addEventListener('pointerdown', onDown);
    el.addEventListener('pointermove', onMove);
    el.addEventListener('pointerup', onUp);
    el.addEventListener('pointercancel', onUp);
    // Stop the browser from intercepting the pinch (native zoom) or pan.
    el.style.touchAction = 'none';

    pinchZoomHandlers.set(elementId, { onDown, onMove, onUp });
}

export function unregisterPinchZoom(elementId) {
    const h = pinchZoomHandlers.get(elementId);
    if (!h) return;
    const el = document.getElementById(elementId);
    if (el) {
        el.removeEventListener('pointerdown', h.onDown);
        el.removeEventListener('pointermove', h.onMove);
        el.removeEventListener('pointerup', h.onUp);
        el.removeEventListener('pointercancel', h.onUp);
        el.style.touchAction = '';
    }
    pinchZoomHandlers.delete(elementId);
}

// --- Drawer Swipe ---

const drawerHandlers = new Map();
const drawerSnapHandlers = new Map();

// #381 Codex P1 — the drawer panel itself became a scroll container
// (overflow-y-auto, so tall content stays reachable) alongside these
// touch-driven dismiss/snap gestures, which are ALSO vertical for a
// Top/Bottom drawer — the same axis. Without this check, a finger drag that's
// meant to scroll the panel's own content is indistinguishable from a
// drag meant to close it, and the swipe handler would start dragging the
// whole panel out from under the user's scroll attempt.
//
// vaul's own rule (the reference this drawer follows) is the fix: a
// dismiss/close-direction drag only ever ARMS once the panel's scrollable
// content is already at the boundary the drag would otherwise scroll PAST —
// scrollTop 0 for a "drag down to close" (Bottom drawer), or fully
// scrolled-to-bottom for "drag up to close" (Top drawer). Until that boundary
// is reached, the native scroll wins outright (these listeners are `passive`
// and never call preventDefault, so scrolling was never blocked — only the
// panel's OWN transform-dragging needed gating). `sign` is each caller's own
// dismiss-direction constant (+1 = drag-down closes, -1 = drag-up closes) —
// same convention both registerDrawerSwipe (dismissSign) and
// registerDrawerSnap (sign) already use.
function isScrolledToClosingBoundary(el, sign) {
    return sign > 0
        ? el.scrollTop <= 0
        : el.scrollTop + el.clientHeight >= el.scrollHeight - 1; // 1px rounding slack
}

// #381 Codex P2 (round 2) — a touch that starts on the visual drag handle
// (DrawerContent.razor's [data-drawer-handle]) always arms dismiss regardless
// of the panel's scroll position: the handle sits OUTSIDE the scrollable
// content (it's a sibling of @ChildContent, not part of it), so there is no
// competing "the user meant to scroll" reading for a touch that starts there
// at all — gating it on scrollTop would only ever produce false negatives.
function startedOnDragHandle(target, panelEl) {
    const handle = target.closest && target.closest('[data-drawer-handle]');
    return !!(handle && panelEl.contains(handle));
}

export function registerDrawerSwipe(elementId, direction, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;

    const isHorizontal = direction === 'left' || direction === 'right';
    // Axis configuration: which signed direction along the active axis dismisses the sheet.
    const dismissSign = (direction === 'down' || direction === 'right') ? +1 : -1;
    // 3.0.1 — allow C# (LumeoGestureOptions) to override the previously
    // hardcoded thresholds. Null/undefined keeps the historical defaults.
    const activationOverride = options && options.activationPx;
    const fireOverride = options && options.firePx;

    // 2.1.3 UX tuning (was: element followed the finger from pixel 1 with no
    // axis lock and no rubber-banding — felt wackelig under micro-finger
    // movement and accidentally engaged on diagonal scroll attempts):
    //
    // 1. ACTIVATION_THRESHOLD — finger must travel this many pixels on the
    //    active axis before the sheet starts to follow. Kills micro-jitter
    //    from settling fingers and edge-of-screen rubber-banding pixels.
    //    Aligned with iOS's gesture lock-in (~10px). Once activated, the
    //    visual translate is measured FROM the threshold so the sheet eases
    //    in smoothly instead of jumping 10px on activation.
    //
    // 2. Direction lock — at activation we lock the gesture to whichever
    //    axis (horizontal vs vertical) the finger moved most along. If the
    //    locked axis doesn't match the configured swipe direction, the
    //    gesture is ignored (the sheet stays put, the inner content can
    //    scroll normally). Prevents diagonal-swipe dismisses when the user
    //    just wanted to scroll content inside a bottom sheet.
    //
    // 3. Rubber-band — past RUBBER_BAND_START the translation compresses
    //    (effectiveDelta * RUBBER_BAND_FACTOR) so the sheet doesn't fly
    //    far off-screen if the user keeps dragging.
    const ACTIVATION_THRESHOLD = (typeof activationOverride === 'number') ? activationOverride : 10;
    const DISMISS_THRESHOLD = (typeof fireOverride === 'number') ? fireOverride : 100;
    const RUBBER_BAND_START = 150;
    const RUBBER_BAND_FACTOR = 0.5;
    // 3.19 — velocity/flick dismiss. A fast flick in the dismiss direction
    // closes even if the raw distance never reached DISMISS_THRESHOLD, which is
    // how a native bottom-sheet feels. px/ms; 0 (or absent) keeps the historical
    // distance-only behaviour. Measured over the last VELOCITY_WINDOW_MS of move
    // samples so a slow drag that ends with a tiny twitch doesn't false-fire.
    const velocityOverride = options && options.velocity;
    const DISMISS_VELOCITY = (typeof velocityOverride === 'number') ? velocityOverride : 0;
    const VELOCITY_WINDOW_MS = 100;

    let startX = 0, startY = 0;
    let currentPos = 0;
    let isDragging = false;
    let active = false;       // gesture passed activation threshold
    let aborted = false;      // axis-lock determined this gesture is for the wrong axis
    let contentOwned = false; // #381 round 2 — see its own remarks below
    let startedOnHandle = false;
    let samples = [];         // recent {pos, t} on the active axis for velocity

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
        currentPos = isHorizontal ? startX : startY;
        isDragging = true;
        active = false;
        aborted = false;
        contentOwned = false;
        // #381 Codex P2 (round 2) — captured ONCE per touch, at the same
        // point origStart/origEnd-style anchors are captured elsewhere in
        // this file: a touch that starts on the handle always arms dismiss,
        // scroll position never enters into it for that touch.
        startedOnHandle = startedOnDragHandle(e.target, el);
        samples = [{ pos: currentPos, t: performance.now() }];
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging || aborted || contentOwned) return;
        const x = e.touches[0].clientX;
        const y = e.touches[0].clientY;
        const dx = x - startX;
        const dy = y - startY;
        currentPos = isHorizontal ? x : y;

        const now = performance.now();
        samples.push({ pos: currentPos, t: now });
        // Keep only the trailing window so end-velocity reflects the flick,
        // not the whole drag.
        while (samples.length > 2 && now - samples[0].t > VELOCITY_WINDOW_MS) samples.shift();

        if (!active) {
            // Wait for the finger to travel far enough to commit to a gesture,
            // then lock the gesture to the dominant axis. If the dominant axis
            // isn't ours, abort — don't touch the transform, let the inner
            // content scroll.
            const absDx = Math.abs(dx);
            const absDy = Math.abs(dy);
            if (absDx < ACTIVATION_THRESHOLD && absDy < ACTIVATION_THRESHOLD) return;
            const dominantHorizontal = absDx > absDy;
            if (dominantHorizontal !== isHorizontal) {
                aborted = true;
                return;
            }
            // #381 Codex P1/P2 (round 2) — see isScrolledToClosingBoundary's own
            // remarks (Top/Bottom drawer: dismiss and scroll share the vertical
            // axis) and startedOnDragHandle's (the handle always bypasses this
            // gate entirely). Only gates the CLOSING direction: a drag the wrong
            // way (e.g. swiping up on a bottom drawer) never actually drags the
            // panel anyway (the sign !== dismissSign branch below already resets
            // the transform to a no-op every frame), so it can't fight scrolling
            // regardless of this check.
            //
            // Round 2 fix: this is now a ONE-TIME decision for the WHOLE touch,
            // not just the current event. The original version only `return`ed
            // from this single call — a LATER move within the SAME touch (after
            // native scroll carries the panel to the boundary mid-gesture) would
            // re-evaluate true and let dismiss arm mid-scroll, which read as the
            // drawer suddenly starting to close while the user was still
            // scrolling. `contentOwned` latches the verdict instead: once a
            // touch is decided to begin away from the boundary, it stays
            // content-owned — no dismiss/drag logic at all — until the finger
            // lifts and a NEW touch starts (onTouchStart resets it).
            if (!isHorizontal && !startedOnHandle) {
                const closingDirection = Math.sign(dy) === dismissSign;
                if (closingDirection && !isScrolledToClosingBoundary(el, dismissSign)) {
                    contentOwned = true;
                    return;
                }
            }
            active = true;
        }

        // Measure travel along our axis, subtract the threshold so the visual
        // translate starts at 0 right after activation (no 10px jump).
        const axisDelta = isHorizontal ? dx : dy;
        const sign = Math.sign(axisDelta);
        if (sign !== dismissSign) {
            // Wrong direction on the right axis (e.g. swiping up on a bottom
            // sheet whose dismiss is "down") — snap back to 0, don't drag the
            // sheet against the dismiss direction.
            el.style.transform = '';
            return;
        }
        let effective = axisDelta - sign * ACTIVATION_THRESHOLD;

        // Rubber-band past the threshold so the sheet doesn't fly far off
        // when the user keeps pulling.
        const absEffective = Math.abs(effective);
        if (absEffective > RUBBER_BAND_START) {
            const excess = absEffective - RUBBER_BAND_START;
            effective = sign * (RUBBER_BAND_START + excess * RUBBER_BAND_FACTOR);
        }

        if (isHorizontal) {
            el.style.transform = `translateX(${effective}px)`;
        } else {
            el.style.transform = `translateY(${effective}px)`;
        }
    };

    const onTouchEnd = () => {
        if (!isDragging) return;
        const wasActive = active;
        const wasAborted = aborted;
        isDragging = false;
        active = false;
        aborted = false;
        el.style.transition = '';
        if (wasAborted || !wasActive) {
            // Either the gesture was locked to the wrong axis or never even
            // crossed the activation threshold — leave the sheet where it
            // started, do nothing.
            el.style.transform = '';
            return;
        }
        const delta = currentPos - (isHorizontal ? startX : startY);
        // End velocity (px/ms) along the active axis, measured over the trailing
        // sample window so it reflects the release flick rather than the whole drag.
        let velocity = 0;
        if (samples.length >= 2) {
            const last = samples[samples.length - 1];
            const first = samples[0];
            const dt = last.t - first.t;
            if (dt > 0) velocity = (last.pos - first.pos) / dt;
        }
        // Dismiss threshold is measured on raw axis delta (intent), not the
        // rubber-banded visual translate, so the gesture feels predictable
        // regardless of how far the rubber-band let the sheet travel. A fast
        // flick in the dismiss direction also fires, even below the distance.
        const correctDir = Math.sign(delta) === dismissSign;
        const farEnough = Math.abs(delta) > DISMISS_THRESHOLD;
        const fastEnough = DISMISS_VELOCITY > 0 && Math.sign(velocity) === dismissSign && Math.abs(velocity) >= DISMISS_VELOCITY;
        const shouldDismiss = correctDir && (farEnough || fastEnough);
        if (shouldDismiss) {
            dotnetRef.invokeMethodAsync('OnSwipeDismiss', elementId);
        } else {
            el.style.transform = '';
        }
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchmove', onTouchMove, { passive: true });
    el.addEventListener('touchend', onTouchEnd);

    drawerHandlers.set(elementId, { onTouchStart, onTouchMove, onTouchEnd });
}

export function unregisterDrawerSwipe(elementId) {
    const handlers = drawerHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('touchstart', handlers.onTouchStart);
            el.removeEventListener('touchmove', handlers.onTouchMove);
            el.removeEventListener('touchend', handlers.onTouchEnd);
            el.style.transform = '';
        }
        drawerHandlers.delete(elementId);
    }
}

// --- Drawer Snap Points (vaul-style) ---
//
// A drawer with snap points rests at one of several fractional heights
// (e.g. [0.4, 0.75, 1] = 40% / 75% / fully open) instead of only open/closed.
// Dragging moves between snaps; on release it settles to the nearest snap
// (velocity-biased), and dragging/flicking below the lowest snap dismisses.
// Vertical only — the C# side calls this only for Top/Bottom drawers; Left/Right
// keep the plain swipe-to-dismiss path above.
export function registerDrawerSnap(elementId, direction, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;

    const snapPoints = (options && Array.isArray(options.snapPoints)) ? options.snapPoints.slice() : [];
    if (snapPoints.length === 0) return;

    // dismissSign: +1 = a bottom drawer hides by translating DOWN; -1 = a top
    // drawer hides by translating UP. The same sign drives "more closed".
    const sign = (direction === 'up') ? -1 : +1;
    const EASING = 'transform 0.32s cubic-bezier(0.32, 0.72, 0, 1)';
    const velocityOverride = options && options.velocity;
    // Honor an explicit 0 (distance-only), matching the swipe path — only a
    // non-number falls back to the default. (#345 review)
    const DISMISS_VELOCITY = (typeof velocityOverride === 'number') ? velocityOverride : 0.4;
    const hasVelocityDismiss = DISMISS_VELOCITY > 0;
    const VELOCITY_WINDOW_MS = 100;
    const DISMISS_FRACTION = 0.5; // drag this far from the lowest snap toward closed → dismiss
    // A protected drawer (PreventClose) still snaps between points; it just
    // never dismisses — dragging past the lowest snap settles back there. (#345)
    const dismissAllowed = !(options && options.dismissible === false);

    const lastIndex = snapPoints.length - 1;
    let activeIndex = (options && Number.isInteger(options.activeIndex))
        ? Math.max(0, Math.min(lastIndex, options.activeIndex))
        : lastIndex;

    let H = el.offsetHeight || 0;
    const offsetFor = (i) => sign * H * (1 - snapPoints[i]);
    const closedOffset = () => sign * H;

    // Open sequence: commit the fully-closed transform synchronously (before
    // the browser paints), then rAF up to the active snap. JS owns the
    // transform for the drawer's whole lifetime, so Blazor re-renders (which
    // don't write transform) never reset it.
    H = el.offsetHeight || H;
    el.style.transition = 'none';
    el.style.transform = `translateY(${closedOffset()}px)`;
    void el.offsetHeight; // force reflow so the closed state is the paint baseline
    requestAnimationFrame(() => {
        el.style.transition = EASING;
        el.style.transform = `translateY(${offsetFor(activeIndex)}px)`;
    });

    let startY = 0, baseOffset = 0, isDragging = false, samples = [];
    let contentOwned = false; // #381 round 2 — see registerDrawerSwipe's own remarks
    let startedOnHandle = false;

    const clampOffset = (off) => {
        const openLimit = offsetFor(lastIndex);   // most-open snap
        const closed = closedOffset();            // fully hidden
        // Don't allow dragging more open than the top snap, nor past fully closed.
        let lo = Math.min(openLimit, closed), hi = Math.max(openLimit, closed);
        return Math.max(lo, Math.min(hi, off));
    };

    const onTouchStart = (e) => {
        H = el.offsetHeight || H;
        startY = e.touches[0].clientY;
        baseOffset = offsetFor(activeIndex);
        isDragging = true;
        contentOwned = false;
        startedOnHandle = startedOnDragHandle(e.target, el);
        samples = [{ pos: startY, t: performance.now() }];
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging || contentOwned) return;
        const y = e.touches[0].clientY;
        const now = performance.now();
        samples.push({ pos: y, t: now });
        while (samples.length > 2 && now - samples[0].t > VELOCITY_WINDOW_MS) samples.shift();

        // #381 Codex P1/P2 (round 2) — see isScrolledToClosingBoundary's own
        // remarks (shared with registerDrawerSwipe) and startedOnDragHandle's
        // (the handle always bypasses this gate entirely). This is a ONE-TIME
        // decision for the WHOLE touch, not just the current event: the
        // original version rebased startY/baseOffset and returned, which let a
        // LATER move within the SAME touch (once native scroll reached the
        // boundary mid-gesture) re-arm the drag — the panel would suddenly
        // start closing mid-scroll. `contentOwned` latches the verdict instead
        // (checked at the top of this function): once a touch is decided to
        // begin away from the boundary, this gesture never drags the panel at
        // all — no rebase-and-retry, no re-arm — until the finger lifts and a
        // NEW touch starts (onTouchStart resets it).
        if (!startedOnHandle) {
            const closingDirection = Math.sign(y - startY) === sign;
            if (closingDirection && !isScrolledToClosingBoundary(el, sign)) {
                contentOwned = true;
                return;
            }
        }

        const proposed = clampOffset(baseOffset + (y - startY));
        el.style.transform = `translateY(${proposed}px)`;
    };

    const onTouchEnd = (e) => {
        if (!isDragging) return;
        isDragging = false;
        // The panel's transform was never touched for a content-owned touch
        // (the gate above returns before ever setting it) — nothing to settle
        // or dismiss; the drawer stays exactly at its last committed snap.
        if (contentOwned) return;
        const endY = (e.changedTouches && e.changedTouches[0]) ? e.changedTouches[0].clientY : startY;
        const currentOffset = clampOffset(baseOffset + (endY - startY));

        let velocity = 0;
        if (samples.length >= 2) {
            const a = samples[0], b = samples[samples.length - 1];
            const dt = b.t - a.t;
            if (dt > 0) velocity = (b.pos - a.pos) / dt;
        }
        const flickDismiss = hasVelocityDismiss && Math.sign(velocity) === sign && Math.abs(velocity) >= DISMISS_VELOCITY;
        const flickOpen = hasVelocityDismiss && Math.sign(velocity) === -sign && Math.abs(velocity) >= DISMISS_VELOCITY;

        // Distance dragged past the lowest snap, toward fully closed.
        const lowest = offsetFor(0);
        const distPastLowest = (currentOffset - lowest) * sign;
        const gapToClosed = Math.abs(closedOffset() - lowest) || 1;

        // Would this release close the drawer — a flick-down at/below the lowest
        // snap, or dragged most of the way past it?
        const wantDismiss =
            (flickDismiss && (activeIndex === 0 || distPastLowest > 0)) ||
            (!flickDismiss && !flickOpen && distPastLowest > gapToClosed * DISMISS_FRACTION);

        let dismiss = false;
        let targetIndex = activeIndex;
        if (wantDismiss) {
            if (dismissAllowed) dismiss = true;
            else targetIndex = 0;          // protected: settle at the lowest snap instead of closing
        } else if (flickDismiss) {
            targetIndex = Math.max(0, activeIndex - 1);
        } else if (flickOpen) {
            targetIndex = Math.min(lastIndex, activeIndex + 1);
        } else {
            // Settle to the nearest snap by position.
            let best = 0, bestDist = Infinity;
            for (let i = 0; i < snapPoints.length; i++) {
                const d = Math.abs(currentOffset - offsetFor(i));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            targetIndex = best;
        }

        el.style.transition = EASING;
        if (dismiss) {
            el.style.transform = `translateY(${closedOffset()}px)`;
            // Respect OnBeforeClose: if C# vetoes the dismiss, snap back to the
            // active snap instead of leaving the panel translated off-screen. (#345)
            Promise.resolve(dotnetRef.invokeMethodAsync('OnDrawerSnapDismiss', elementId))
                .then(ok => {
                    if (ok === false) {
                        el.style.transition = EASING;
                        el.style.transform = `translateY(${offsetFor(activeIndex)}px)`;
                    }
                })
                .catch(() => {});
        } else {
            el.style.transform = `translateY(${offsetFor(targetIndex)}px)`;
            if (targetIndex !== activeIndex) {
                activeIndex = targetIndex;
                dotnetRef.invokeMethodAsync('OnDrawerSnapChange', elementId, targetIndex);
            }
        }
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchmove', onTouchMove, { passive: true });
    el.addEventListener('touchend', onTouchEnd);

    drawerSnapHandlers.set(elementId, {
        onTouchStart, onTouchMove, onTouchEnd,
        // setActive lets C# move the drawer programmatically (two-way ActiveSnapPoint).
        setActive(i) {
            if (!Number.isInteger(i) || i < 0 || i > lastIndex || i === activeIndex) return;
            activeIndex = i;
            H = el.offsetHeight || H;
            el.style.transition = EASING;
            el.style.transform = `translateY(${offsetFor(i)}px)`;
        }
    });
}

export function setDrawerSnap(elementId, index) {
    const h = drawerSnapHandlers.get(elementId);
    if (h) h.setActive(index);
}

export function unregisterDrawerSnap(elementId) {
    const handlers = drawerSnapHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('touchstart', handlers.onTouchStart);
            el.removeEventListener('touchmove', handlers.onTouchMove);
            el.removeEventListener('touchend', handlers.onTouchEnd);
            el.style.transform = '';
            el.style.transition = '';
        }
        drawerSnapHandlers.delete(elementId);
    }
}

// --- Carousel Swipe ---

const carouselHandlers = new Map();

export function registerCarouselSwipe(elementId, orientation, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;
    // 3.0.1 — options exist for parity with the other swipe registrars and to
    // support future Carousel-specific thresholds. The current Carousel
    // implementation relies on CSS scroll-snap for momentum, so the values
    // are accepted but not yet consumed; wiring them in won't be a breaking
    // change for callsites that already pass the bag.
    void options;

    // Compute the nearest child index from the current scroll position.
    const getNearestIndex = () => {
        const children = el.children;
        if (!children.length) return 0;
        const scrollPos = orientation === 'horizontal' ? el.scrollLeft : el.scrollTop;
        let best = 0, bestDist = Infinity;
        for (let i = 0; i < children.length; i++) {
            const offset = orientation === 'horizontal'
                ? children[i].offsetLeft
                : children[i].offsetTop;
            const dist = Math.abs(offset - scrollPos);
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    };

    const onScroll = () => {
        const scrollPos = orientation === 'horizontal' ? el.scrollLeft : el.scrollTop;
        const maxScroll = orientation === 'horizontal'
            ? el.scrollWidth - el.clientWidth
            : el.scrollHeight - el.clientHeight;
        const nearestIndex = getNearestIndex();
        dotnetRef.invokeMethodAsync('OnScrollPosition', elementId, scrollPos, maxScroll, nearestIndex);
    };

    // Touch swipe is handled entirely by CSS scroll-snap — no imperative scrollIntoView
    // on touchend, which would fight the browser's momentum animation and cause jitter.
    // We still detect swipe direction so the .NET layer can sync _currentIndex immediately,
    // but we do NOT call CarouselScrollTo; the snap takes care of positioning.
    let startX = 0, startY = 0;

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('scroll', onScroll, { passive: true });

    carouselHandlers.set(elementId, { onTouchStart, onScroll });
}

export function unregisterCarouselSwipe(elementId) {
    const handlers = carouselHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('touchstart', handlers.onTouchStart);
            el.removeEventListener('scroll', handlers.onScroll);
        }
        carouselHandlers.delete(elementId);
    }
}

export function carouselScrollTo(elementId, index, behavior) {
    const el = document.getElementById(elementId);
    if (!el) return;
    const children = el.children;
    if (children.length === 0) return;
    // C# sends int.MaxValue as a "wrap to last slide" sentinel (Loop mode) —
    // the child count only exists on this side of the interop boundary.
    if (index >= children.length) index = children.length - 1;
    if (index >= 0) {
        children[index].scrollIntoView({ behavior: behavior || 'smooth', block: 'nearest', inline: 'start' });
    }
}

// --- Selective keydown preventDefault ---
// Blazor's @onkeydown:preventDefault directive is all-or-nothing and its
// bool form is evaluated at render time (one event late). Components that
// must suppress the default for SOME keys only (Splitter dividers: arrows
// but never Tab; PromptInput: Enter but not Shift+Enter / IME-confirm)
// register the exact rules here so the decision happens synchronously in
// the real keydown dispatch.

// Keyed by the id it was REGISTERED under, but the value carries the actual DOM
// element reference alongside the handler — not just the handler alone. Blazor
// patches the id="" attribute on an element IN PLACE (same DOM node, new id
// value) during render, which happens BEFORE the C# OnAfterRenderAsync callback
// that calls unregisterPreventDefaultKeys(oldId) below ever runs. So by the time
// that call arrives, document.getElementById(oldId) already returns null — the
// node now answers to the NEW id — and a re-lookup-by-id would silently fail to
// find the element, leaving its keydown listener attached forever (never
// removed, no longer reachable through this map) while a second listener gets
// added under the new id. Storing the element reference at register time lets
// unregister detach the listener from the exact node it was attached to,
// independent of whatever id that node carries now (PR #356 round-5, Codex P2:
// PopoverTrigger's stale Space-suppressor kept firing on the child control
// after an id change).
const preventDefaultKeyHandlers = new Map();

export function registerPreventDefaultKeys(elementId, rules) {
    unregisterPreventDefaultKeys(elementId);
    const el = document.getElementById(elementId);
    if (!el) return;
    const handler = (e) => {
        for (const r of rules) {
            if (e.key !== r.key) continue;
            if (r.requireNoModifiers && (e.shiftKey || e.ctrlKey || e.altKey || e.metaKey)) continue;
            // keyCode 229 covers engines that fire composition keydowns
            // without setting isComposing.
            if (r.skipComposing && (e.isComposing || e.keyCode === 229)) continue;
            // skipEditable exemptions:
            //  - text-editable fields (input/textarea/select/contenteditable) are exempt for EVERY key:
            //    typing must win over the container's key handling.
            //  - interactive controls (button/a/role=button) are exempt ONLY for ACTIVATION keys
            //    (Space/Enter) so their native activation isn't cancelled (Codex P2) — but the container
            //    STILL suppresses Arrow/Home/End/Page on them: otherwise e.g. AudioPlayer's ArrowLeft/Right
            //    seek (registered with skipEditable) would also let the browser scroll the page when focus
            //    is on a transport button (Codex P2, round-12 regression of the round-9 button exemption).
            if (r.skipEditable && e.target instanceof Element) {
                if (e.target.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]')) continue;
                const isActivation = e.key === ' ' || e.key === 'Spacebar' || e.key === 'Enter';
                if (isActivation && e.target.closest('button, a[href], [role="button"]')) continue;
            }
            e.preventDefault();
            return;
        }
    };
    el.addEventListener('keydown', handler);
    preventDefaultKeyHandlers.set(elementId, { el, handler });
}

export function unregisterPreventDefaultKeys(elementId) {
    const entry = preventDefaultKeyHandlers.get(elementId);
    if (entry) {
        entry.el.removeEventListener('keydown', entry.handler);
        preventDefaultKeyHandlers.delete(elementId);
    }
}

// --- Horizontal Swipe (Calendar month navigation) ---
// threshold: 50px horizontal, < 40px vertical (so vertical scroll still works)

const horizontalSwipeHandlers = new Map();

export function registerHorizontalSwipe(elementId, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // 3.0.1 — read overrides from LumeoGestureOptions bag passed by C#.
    const swipeThreshold = (options && typeof options.swipeThresholdPx === 'number') ? options.swipeThresholdPx : 50;
    const verticalDeadZone = (options && typeof options.verticalDeadZonePx === 'number') ? options.verticalDeadZonePx : 40;

    // Allow vertical page scroll while detecting horizontal swipes.
    el.style.touchAction = 'pan-y';

    let startX = 0, startY = 0;

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
    };

    const onTouchEnd = (e) => {
        const deltaX = e.changedTouches[0].clientX - startX;
        const deltaY = e.changedTouches[0].clientY - startY;
        if (Math.abs(deltaY) >= verticalDeadZone) return; // too much vertical — ignore
        if (Math.abs(deltaX) < swipeThreshold) return;    // below horizontal threshold — ignore
        const rtl = getComputedStyle(el).direction === 'rtl';
        const forward = rtl ? deltaX > 0 : deltaX < 0; // RTL mirrors physical deltaX
        dotnetRef.invokeMethodAsync('OnCalendarSwipe', elementId, forward ? 'next' : 'prev');
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchend', onTouchEnd);

    horizontalSwipeHandlers.set(elementId, { onTouchStart, onTouchEnd });
}

export function unregisterHorizontalSwipe(elementId) {
    const handlers = horizontalSwipeHandlers.get(elementId);
    if (!handlers) return;
    const el = document.getElementById(elementId);
    if (el) {
        el.removeEventListener('touchstart', handlers.onTouchStart);
        el.removeEventListener('touchend', handlers.onTouchEnd);
        el.style.touchAction = '';
    }
    horizontalSwipeHandlers.delete(elementId);
}

// --- Gallery Swipe (ImageGallery fullscreen prev/next) ---
// threshold: 60px horizontal, < 40px vertical (so vertical scroll still works).
// Calls back with 'next' or 'prev' via the configurable methodName.

const gallerySwipeHandlers = new Map();

export function registerGallerySwipe(elementId, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // 3.0.1 — read overrides from LumeoGestureOptions bag passed by C#.
    const swipeThreshold = (options && typeof options.swipeThresholdPx === 'number') ? options.swipeThresholdPx : 60;
    const verticalDeadZone = (options && typeof options.verticalDeadZonePx === 'number') ? options.verticalDeadZonePx : 40;

    // Clean up any previous registration on the same element.
    const prev = gallerySwipeHandlers.get(elementId);
    if (prev) {
        el.removeEventListener('touchstart', prev.onTouchStart);
        el.removeEventListener('touchend', prev.onTouchEnd);
    }

    // Allow vertical page scroll while detecting horizontal swipes.
    el.style.touchAction = 'pan-y';

    let startX = 0, startY = 0;

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
    };

    const onTouchEnd = (e) => {
        const deltaX = e.changedTouches[0].clientX - startX;
        const deltaY = e.changedTouches[0].clientY - startY;
        if (Math.abs(deltaY) >= verticalDeadZone) return; // too much vertical — ignore
        if (Math.abs(deltaX) < swipeThreshold) return;    // below horizontal threshold — ignore
        const rtl = getComputedStyle(el).direction === 'rtl';
        const forward = rtl ? deltaX > 0 : deltaX < 0; // RTL mirrors physical deltaX
        dotnetRef.invokeMethodAsync('OnGallerySwipe', elementId, forward ? 'next' : 'prev');
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchend', onTouchEnd);

    gallerySwipeHandlers.set(elementId, { onTouchStart, onTouchEnd });
}

export function unregisterGallerySwipe(elementId) {
    const handlers = gallerySwipeHandlers.get(elementId);
    if (!handlers) return;
    const el = document.getElementById(elementId);
    if (el) {
        el.removeEventListener('touchstart', handlers.onTouchStart);
        el.removeEventListener('touchend', handlers.onTouchEnd);
        el.style.touchAction = '';
    }
    gallerySwipeHandlers.delete(elementId);
}

// --- Tab Swipe ---
// Horizontal pointer drag (> 50 px horizontal, < 30 px vertical) on a TabsContent
// panel triggers next/prev tab navigation. Uses pointer events + setPointerCapture
// so it works on both touch and mouse without stealing vertical scroll.

const tabSwipeHandlers = new Map();

export function registerTabSwipe(elementId, wrap, dotnetRef, options) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // 3.0.1 — read overrides from LumeoGestureOptions bag passed by C#.
    // The historical vertical drift cap was 30px; the global default in
    // LumeoGestureOptions is 40px, so unless the consumer overrides we
    // keep the historical 30px for Tab swipes to preserve feel.
    const swipeThreshold = (options && typeof options.swipeThresholdPx === 'number') ? options.swipeThresholdPx : 50;
    const verticalDeadZone = (options && typeof options.verticalDeadZonePx === 'number') ? options.verticalDeadZonePx : 30;

    // Allow vertical scroll; we only care about horizontal swipes.
    el.style.touchAction = 'pan-y';

    let startX = 0, startY = 0, pointerId = null;
    let tracking = false;

    const onPointerDown = (e) => {
        // Ignore swipes that start on interactive children.
        if (e.target.closest('button, input, textarea, select, [contenteditable]')) return;
        startX = e.clientX;
        startY = e.clientY;
        pointerId = e.pointerId;
        tracking = true;
        try { el.setPointerCapture(e.pointerId); } catch (_) {}
    };

    const onPointerUp = (e) => {
        if (!tracking || e.pointerId !== pointerId) return;
        tracking = false;
        try { el.releasePointerCapture(e.pointerId); } catch (_) {}

        const deltaX = e.clientX - startX;
        const deltaY = e.clientY - startY;
        if (Math.abs(deltaY) >= verticalDeadZone) return; // too much vertical drift — ignore
        if (Math.abs(deltaX) < swipeThreshold) return;    // below horizontal threshold — ignore

        const rtl = getComputedStyle(el).direction === 'rtl';
        const forward = rtl ? deltaX > 0 : deltaX < 0; // RTL mirrors physical deltaX
        const direction = forward ? 'next' : 'prev';
        dotnetRef.invokeMethodAsync('OnTabSwipe', elementId, direction);
    };

    const onPointerCancel = (e) => {
        if (e.pointerId === pointerId) tracking = false;
    };

    el.addEventListener('pointerdown', onPointerDown);
    el.addEventListener('pointerup', onPointerUp);
    el.addEventListener('pointercancel', onPointerCancel);

    tabSwipeHandlers.set(elementId, { onPointerDown, onPointerUp, onPointerCancel });
}

export function unregisterTabSwipe(elementId) {
    const handlers = tabSwipeHandlers.get(elementId);
    if (!handlers) return;
    const el = document.getElementById(elementId);
    if (el) {
        el.removeEventListener('pointerdown', handlers.onPointerDown);
        el.removeEventListener('pointerup', handlers.onPointerUp);
        el.removeEventListener('pointercancel', handlers.onPointerCancel);
        el.style.touchAction = '';
    }
    tabSwipeHandlers.delete(elementId);
}

// --- Resizable Handle ---

const resizeHandlers = new Map();

// Resizable panel-group handle. Originally listened to mousedown/mousemove/
// mouseup on document, so touch users could not drag the divider at all
// (mouse events are only synthesised AFTER a touch sequence ends, never
// during). Migrated to pointer events with setPointerCapture so a single
// code path serves mouse, pen and touch — matching what Splitter/Window/
// AudioPlayer already do. `touch-action: none` on the handle hands the
// gesture to us instead of the browser interpreting it as a vertical scroll.
export function registerResizeHandle(elementId, direction, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    el.style.touchAction = 'none';

    let isDragging = false;
    let startPos = 0;
    let activePointerId = null;

    const onPointerDown = (e) => {
        // Ignore secondary buttons (right-click, middle-click) so the divider
        // doesn't grab a context-menu attempt.
        if (e.button !== undefined && e.button !== 0) return;
        isDragging = true;
        activePointerId = e.pointerId;
        startPos = direction === 'horizontal' ? e.clientX : e.clientY;
        document.body.style.cursor = direction === 'horizontal' ? 'col-resize' : 'row-resize';
        document.body.style.userSelect = 'none';
        try { el.setPointerCapture(e.pointerId); } catch { /* not all browsers/contexts */ }
        e.preventDefault();
    };

    const onPointerMove = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        const currentPos = direction === 'horizontal' ? e.clientX : e.clientY;
        const delta = currentPos - startPos;
        startPos = currentPos;
        dotnetRef.invokeMethodAsync('OnResize', elementId, delta);
    };

    const onPointerUp = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        isDragging = false;
        activePointerId = null;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        try { el.releasePointerCapture(e.pointerId); } catch { /* fallthrough */ }
        dotnetRef.invokeMethodAsync('OnResizeEnd', elementId);
    };

    // pointermove + pointerup go on the element itself: setPointerCapture
    // guarantees subsequent events route to it even when the finger drifts
    // off the (visually 1 px wide) divider during a fast drag.
    el.addEventListener('pointerdown', onPointerDown);
    el.addEventListener('pointermove', onPointerMove);
    el.addEventListener('pointerup', onPointerUp);
    el.addEventListener('pointercancel', onPointerUp);

    resizeHandlers.set(elementId, { onPointerDown, onPointerMove, onPointerUp });
}

export function unregisterResizeHandle(elementId) {
    const handlers = resizeHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('pointerdown', handlers.onPointerDown);
            el.removeEventListener('pointermove', handlers.onPointerMove);
            el.removeEventListener('pointerup', handlers.onPointerUp);
            el.removeEventListener('pointercancel', handlers.onPointerUp);
        }
        resizeHandlers.delete(elementId);
    }
}

// --- Keyboard Shortcuts ---

let shortcutDotnetRef = null;
const shortcuts = new Map();

export function registerKeyboardShortcuts(dotnetRef) {
    shortcuts.clear();  // rc.44: drop stale entries from prior circuit on reconnect
    shortcutDotnetRef = dotnetRef;
    if (!window.__lumeoKbdListener) {
        window.__lumeoKbdListener = (e) => {
            const tag = (e.target?.tagName || '').toUpperCase();
            const isEditable = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || e.target?.isContentEditable;
            for (const [id, { combo, preventDefault, allowInEditable }] of shortcuts) {
                // Inside an editable element, skip EVERY shortcut that has not explicitly
                // opted in via allowInEditable. Previously only modifier-LESS shortcuts were
                // skipped, so a modifier combo like Ctrl/Cmd+B toggled a registered handler
                // (e.g. the sidebar) AND preventDefault'd the browser's native bold while the
                // user was typing. Global shortcuts that must fire everywhere (a Ctrl/Cmd+K
                // command palette) register with allowInEditable:true and are exempt here.
                if (isEditable && !allowInEditable) continue;
                if (matchesCombo(e, combo)) {
                    if (preventDefault) e.preventDefault();
                    shortcutDotnetRef?.invokeMethodAsync('OnShortcutTriggered', id);
                    return;
                }
            }
        };
        document.addEventListener('keydown', window.__lumeoKbdListener);
    }
}

export function unregisterKeyboardShortcuts() {
    if (window.__lumeoKbdListener) {
        document.removeEventListener('keydown', window.__lumeoKbdListener);
        window.__lumeoKbdListener = null;
    }
    shortcuts.clear();
    shortcutDotnetRef = null;
}

export function addShortcut(id, combo, preventDefault, allowInEditable) {
    shortcuts.set(id, { combo, preventDefault, allowInEditable: !!allowInEditable });
}

export function removeShortcut(id) {
    shortcuts.delete(id);
}

function matchesCombo(e, combo) {
    const parts = combo.split('+');
    const key = parts[parts.length - 1];
    const needCtrl = parts.includes('ctrl');
    const needAlt = parts.includes('alt');
    const needShift = parts.includes('shift');
    const needMeta = parts.includes('meta');

    if (needCtrl !== (e.ctrlKey || e.metaKey)) return false;
    if (needAlt !== e.altKey) return false;
    if (needShift !== e.shiftKey) return false;
    if (needMeta && !e.metaKey) return false;

    return e.key.toLowerCase() === key || e.code.toLowerCase() === key;
}

// --- Scrollspy ---

const scrollspyHandlers = new Map();

function findScrollableViewport(container) {
    // Prefer Lumeo ScrollArea viewport
    const scrollArea = container.querySelector('[data-slot="scroll-area-viewport"]');
    if (scrollArea) return scrollArea;

    // Find first child with overflow scrolling
    const scrollable = container.querySelector('[style*="overflow"], .overflow-y-auto, .overflow-auto, .overflow-y-scroll');
    if (scrollable) return scrollable;

    // Fallback: find first descendant that is actually scrollable
    const children = container.querySelectorAll('*');
    for (const child of children) {
        const style = window.getComputedStyle(child);
        if ((style.overflowY === 'auto' || style.overflowY === 'scroll') && child.scrollHeight > child.clientHeight) {
            return child;
        }
    }

    return container;
}

export function registerScrollspy(containerId, offset, smooth, dotnetRef) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const viewport = findScrollableViewport(container);

    const onScroll = () => {
        const sections = container.querySelectorAll('[data-scrollspy-section]');
        if (sections.length === 0) return;

        const scrollTop = viewport.scrollTop;
        let activeId = null;
        let minDelta = Infinity;

        for (const section of sections) {
            const top = section.offsetTop - offset;
            if (top <= scrollTop + 10) {
                const delta = scrollTop - top;
                if (delta >= 0 && delta < minDelta) {
                    minDelta = delta;
                    activeId = section.id;
                }
            }
        }

        // If scrolled to bottom, activate last section
        const isAtBottom = viewport.scrollTop + viewport.clientHeight >= viewport.scrollHeight - 5;
        if (isAtBottom && sections.length > 0) {
            activeId = sections[sections.length - 1].id;
        }

        if (activeId === null && sections.length > 0) {
            activeId = sections[0].id;
        }

        dotnetRef.invokeMethodAsync('OnScrollspyUpdate', containerId, activeId);
    };

    viewport.addEventListener('scroll', onScroll, { passive: true });
    scrollspyHandlers.set(containerId, { viewport, onScroll });

    // Initial check
    requestAnimationFrame(onScroll);
}

export function unregisterScrollspy(containerId) {
    const handler = scrollspyHandlers.get(containerId);
    if (handler) {
        handler.viewport.removeEventListener('scroll', handler.onScroll);
        scrollspyHandlers.delete(containerId);
    }
}

export function scrollspyScrollTo(containerId, sectionId, smooth, offset = 0) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const viewport = findScrollableViewport(container);
    const section = document.getElementById(sectionId);
    if (!section) return;

    // Honour the same Offset the observer uses to decide the active section
    // (#246): subtract it so a click lands the section top below a sticky
    // header instead of flush with the viewport top (which would re-activate
    // the *previous* section). Clamp at 0 so we never request a negative top.
    viewport.scrollTo({
        top: Math.max(0, section.offsetTop - offset),
        behavior: smooth ? 'smooth' : 'auto'
    });
}

// --- Tabs overflow scroll arrows (#239) ---

const tabsOverflowHandlers = new Map();

export function registerTabsOverflow(listId, dotnetRef) {
    // Idempotent: drop any prior listeners/observer for this id so a re-register
    // (e.g. ShowArrows toggled off→on) never stacks duplicate handlers.
    unregisterTabsOverflow(listId);
    const el = document.getElementById(listId);
    if (!el) return;

    const report = () => {
        // Horizontal tablists scroll on X, vertical on Y. A 1px slack absorbs
        // sub-pixel rounding so the end arrow hides exactly at the end.
        const horizontal = el.scrollWidth > el.clientWidth;
        const canStart = horizontal ? el.scrollLeft > 1 : el.scrollTop > 1;
        const canEnd = horizontal
            ? el.scrollLeft < el.scrollWidth - el.clientWidth - 1
            : el.scrollTop < el.scrollHeight - el.clientHeight - 1;
        dotnetRef.invokeMethodAsync('OnTabsOverflowChange', listId, canStart, canEnd);
    };

    el.addEventListener('scroll', report, { passive: true });
    // A ResizeObserver catches container resizes AND content changes (tabs
    // added/removed) that flip whether the list overflows at all.
    let ro = null;
    if (typeof ResizeObserver !== 'undefined') {
        ro = new ResizeObserver(report);
        ro.observe(el);
    }
    window.addEventListener('resize', report, { passive: true });
    tabsOverflowHandlers.set(listId, { el, report, ro });

    requestAnimationFrame(report);
}

export function unregisterTabsOverflow(listId) {
    const h = tabsOverflowHandlers.get(listId);
    if (h) {
        h.el.removeEventListener('scroll', h.report);
        window.removeEventListener('resize', h.report);
        if (h.ro) h.ro.disconnect();
        tabsOverflowHandlers.delete(listId);
    }
}

export function tabsScrollBy(listId, delta, horizontal) {
    const el = document.getElementById(listId);
    if (!el) return;
    if (horizontal) el.scrollBy({ left: delta, behavior: 'smooth' });
    else el.scrollBy({ top: delta, behavior: 'smooth' });
}

// --- Toast Swipe ---

const toastSwipeHandlers = new Map();

export function registerToastSwipe(elementId, toastId, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    let startX = 0;
    let currentX = 0;
    let isDragging = false;

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        currentX = startX;
        isDragging = true;
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging) return;
        currentX = e.touches[0].clientX;
        const deltaX = currentX - startX;
        el.style.transform = `translateX(${deltaX}px)`;
        el.style.opacity = String(Math.max(0, 1 - Math.abs(deltaX) / 200));
    };

    const onTouchEnd = () => {
        if (!isDragging) return;
        isDragging = false;
        el.style.transition = '';
        const deltaX = currentX - startX;
        if (Math.abs(deltaX) > 80) {
            dotnetRef.invokeMethodAsync('OnToastSwipeDismiss', toastId);
        } else {
            el.style.transform = '';
            el.style.opacity = '';
        }
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchmove', onTouchMove, { passive: true });
    el.addEventListener('touchend', onTouchEnd);

    toastSwipeHandlers.set(elementId, { onTouchStart, onTouchMove, onTouchEnd });
}

// --- Auto Resize ---

// elementId -> input-listener function reference. Stored so
// unregisterAutoResize can pass the exact callback to removeEventListener
// (anonymous closures can't be removed) and so a repeat setupAutoResize call
// on the same element doesn't stack handlers.
const autoResizeHandlers = new Map();

export function setupAutoResize(elementId, maxRows) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // De-dupe: tear down any prior listener before installing a fresh one.
    const existing = autoResizeHandlers.get(elementId);
    if (existing) el.removeEventListener('input', existing);

    el.style.overflow = 'hidden';
    el.style.resize = 'none';
    const lineHeight = parseInt(window.getComputedStyle(el).lineHeight) || 20;
    const maxHeight = lineHeight * maxRows;

    const resize = () => {
        el.style.height = 'auto';
        el.style.height = Math.min(el.scrollHeight, maxHeight) + 'px';
        if (el.scrollHeight > maxHeight) {
            el.style.overflow = 'auto';
        } else {
            el.style.overflow = 'hidden';
        }
    };

    el.addEventListener('input', resize);
    autoResizeHandlers.set(elementId, resize);
    resize(); // initial
}

export function unregisterAutoResize(elementId) {
    const handler = autoResizeHandlers.get(elementId);
    if (!handler) return;
    const el = document.getElementById(elementId);
    if (el) el.removeEventListener('input', handler);
    autoResizeHandlers.delete(elementId);
}

export function unregisterToastSwipe(elementId) {
    const handlers = toastSwipeHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('touchstart', handlers.onTouchStart);
            el.removeEventListener('touchmove', handlers.onTouchMove);
            el.removeEventListener('touchend', handlers.onTouchEnd);
            el.style.transform = '';
            el.style.opacity = '';
        }
        toastSwipeHandlers.delete(elementId);
    }
}

// --- Menu Keyboard Navigation ---

function getMenuItems(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return [];
    return Array.from(container.querySelectorAll('[role="menuitem"]:not([disabled]), [role="menuitemcheckbox"]:not([disabled]), [role="menuitemradio"]:not([disabled]), button:not([disabled]):not([data-no-focus])'));
}

export function focusMenuItemByIndex(containerId, index) {
    const items = getMenuItems(containerId);
    if (index >= 0 && index < items.length) {
        const item = items[index];
        item.focus();
        // Keep the focused item visible in a scrollable menu (long DropdownMenu /
        // Menubar content). block:'nearest' avoids jumping when the item is
        // already on-screen — matches Radix's scroll-into-view-on-highlight.
        if (typeof item.scrollIntoView === 'function') {
            item.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
        return index;
    }
    return -1;
}

export function getMenuItemCount(containerId) {
    return getMenuItems(containerId).length;
}

// Returns the `id` attributes of the descendants of `containerId` matching
// `selector`, in live DOM order. Compound widgets (RadioGroup / ToggleGroup /
// Segmented / Stepper / Splitter, …) call this at navigation time so roving /
// arrow-key / neighbour order tracks the real DOM even after a keyed reorder
// moved reused child instances without re-rendering them. Ids without a value
// are dropped (only registered, addressable items participate in roving nav).
export function getOrderedDescendantIds(containerId, selector) {
    const container = document.getElementById(containerId);
    if (!container) return [];
    return Array.from(container.querySelectorAll(selector))
        .map(el => el.id)
        .filter(id => id);
}

// --- Toolbar roving focus (Radix Toolbar keyboard model) ---
//
// A toolbar is a single tab stop; Arrow keys move focus between its focusable
// items. We resolve the focusable items at call time (so dynamically added/
// removed items are handled) and manage a roving tabindex: the focused item is
// tabindex=0, the rest are tabindex=-1, so Shift+Tab/Tab enter/leave the
// toolbar at the last-focused item.

function getToolbarItems(toolbarId) {
    const container = document.getElementById(toolbarId);
    if (!container) return [];
    // Focusable interactive descendants, excluding disabled ones and the
    // overflow trigger button (it has its own dropdown semantics).
    //
    // The `:not([disabled])` clauses on the button/input/select/textarea arms
    // only exclude an element from THOSE specific arms — they do NOT stop it
    // from ALSO matching the generic `[tabindex]:not([tabindex="-1"])` arm,
    // since querySelectorAll returns the UNION of every comma-separated arm.
    // A roving-active item carries `tabindex="0"` (written by a previous
    // applyToolbarRovingTabindex call) — if it becomes disabled afterward, it
    // still matches that last arm and comes back as a "focusable" item even
    // though it can no longer actually receive focus. That let a disabled
    // former-active item block the empty-fallback branch below (items.length
    // was 1, not 0) while contributing zero real tab stops, so the toolbar
    // silently dropped out of the Tab order entirely (PR #356 round-6, Codex
    // P2). Filtering disabled elements out of the FINAL result — not just
    // individual arms — closes that gap for every arm at once.
    //
    // The `[data-lumeo-toolbar-managed]` arm exists so ANY custom focusable
    // child that is only discoverable via the generic `[tabindex]` arm (e.g.
    // Chip renders a `div role="button"` with no `disabled` attribute) stays
    // discoverable once roving tabindex has touched it. `applyToolbarRovingTabindex`
    // writes `tabindex="-1"` to every INACTIVE item on every call — not just
    // disabled ones — so on the very next call to this function, a merely-
    // inactive Chip would no longer match `[tabindex]:not([tabindex="-1"])`
    // and, having no native tag arm to fall back on, would vanish from the
    // candidate list entirely: permanently unreachable by further roving
    // (Home/End/arrow) even though it was never disabled (PR #356 round-10,
    // Codex P2). Marking every such element the first time it is discovered —
    // active, inactive, or disabled alike — keeps it matching this selector
    // for the lifetime of the toolbar, regardless of what `tabindex` value
    // roving/disabled-handling subsequently writes to it.
    const selector = 'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]), [data-lumeo-toolbar-managed]';
    const candidates = Array.from(container.querySelectorAll(selector))
        .filter(el => !el.closest('[data-toolbar-overflow-trigger]'));
    const items = [];
    for (const el of candidates) {
        // Tag on first sight, before the disabled branch below, so both
        // disabled AND enabled-but-roving-inactive custom items keep matching
        // the `[data-lumeo-toolbar-managed]` arm above on every later call.
        if (!el.hasAttribute('data-lumeo-toolbar-managed')) el.setAttribute('data-lumeo-toolbar-managed', '');
        if (el.disabled || el.getAttribute('aria-disabled') === 'true') {
            // aria-disabled (unlike native `disabled`) does NOT make an element
            // unfocusable — e.g. a clickable disabled Chip renders a focusable
            // div with aria-disabled="true". Excluding it from `items` is not
            // enough on its own: applyToolbarRovingTabindex only ever writes to
            // the elements THIS function returns, so an element that already
            // carries a stale tabindex="0" from a previous roving-active call
            // (it was the active item before becoming aria-disabled) would keep
            // that tabindex forever, leaving it in the Tab order alongside
            // whichever enabled item gets the new roving slot (PR #356 round-7,
            // Codex P2). Clear it here, at the one place that decides an
            // element is excluded, so every call site (initToolbarRoving,
            // moveToolbarFocus, focusToolbarEdge) is covered for free.
            if (el.getAttribute('tabindex') !== '-1') el.setAttribute('tabindex', '-1');
            continue;
        }
        items.push(el);
    }
    return items;
}

function applyToolbarRovingTabindex(items, activeIndex) {
    for (let i = 0; i < items.length; i++) {
        items[i].setAttribute('tabindex', i === activeIndex ? '0' : '-1');
    }
}

// Shared empty-fallback: when a toolbar has no focusable items (yet, or any
// more — e.g. the roving-active item just became disabled/was removed), the
// container itself must stay/become the sole tab stop, or the toolbar
// disappears from the Tab order entirely (PR #356 round-6, Codex P2). Called
// from initToolbarRoving (the render-driven path — Blazor re-renders whenever
// a data-bound `disabled` flips, which is the common case) AND defensively
// from moveToolbarFocus/focusToolbarEdge (the interaction-driven paths), so
// the invariant holds even if items disappear between renders.
function restoreToolbarContainerFallback(toolbarId) {
    const container = document.getElementById(toolbarId);
    if (container) container.setAttribute('tabindex', '0');
}

// True when the currently focused element is text-editable (input/textarea/
// select/contenteditable). getToolbarItems() deliberately includes these tags
// as valid roving-focus stops (a toolbar CAN contain a real text field), but
// arrow/Home/End pressed while one is focused must move the caret/selection,
// not roving focus (PR #356 round-3, Codex P2) — mirrors the skipEditable
// exemption in registerPreventDefaultKeys above.
function isEditableFocusTarget(el) {
    return !!(el && el.closest && el.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]'));
}

// True when an editable `el`'s caret sits at the text boundary in the
// direction `delta` travels (start, delta < 0, for Left/Up; end, delta > 0,
// for Right/Down), with no active selection to collapse first. Only
// input/textarea EVER expose selectionStart/selectionEnd, and even then not
// for every input type — a <select>, a contenteditable element, or an
// input[type=number/email/color/...] has no unambiguous single-caret concept
// (or no selection API at all), so ALL of those are always treated as NOT at
// a boundary (arrows never escape them; Home/End and Tab stay their only way
// out — a deliberate, narrower scope: no known Lumeo toolbar embeds a
// <select>/contenteditable today, and NumberInput's own Up/Down increment/
// decrement handling depends on this, see the opacity check below, PR #356
// round-6).
//
// Chosen contract (PR #356 round-5, Codex P2 — an editable toolbar child made
// every item AFTER it keyboard-unreachable: roving unconditionally deferred
// to the caret and never took the toolbar back over). Mirrors how a
// spreadsheet cell editor or a combobox search field hands off at the text
// edge: the SAME arrow press that lands the caret on the boundary is also the
// press that moves toolbar focus onward — there is no separate "press again
// to escape" step, because by the time this runs the browser's native caret
// move for that press has already happened (skipEditable leaves the key
// unprevented), so pre- and post-press caret state are indistinguishable here.
// A toolbar's embedded field is expected to hold a short, single-purpose
// value (a search box, a label) rather than prose, so trading a rare "I
// wanted to see one more character before leaving" surprise for guaranteeing
// every toolbar item stays reachable is the right default.
function isAtEditableBoundary(el, delta) {
    if (!el || (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA')) return false;
    // input[type=number/email/color/...] doesn't expose a selection API in
    // every engine (Chromium returns null for selectionStart/End on these
    // types) — treat that as boundary-OPAQUE, i.e. NEVER at a boundary, same
    // as the <select>/contenteditable case above. A round-5 version of this
    // returned true here ("escape since we can't introspect"), but for a
    // control like NumberInput's <input type="number">, Up/Down ALSO drive
    // the browser's own increment/decrement — with every press unconditionally
    // treated as a boundary, roving focus escaped the control on the very
    // first Up/Down instead of leaving it to repeatedly adjust the value, and
    // Tab remained the only intentional way to leave (PR #356 round-6, Codex
    // P2). Mirrors the select/contenteditable contract documented above:
    // opaque editables keep the arrow event, they never escape via arrows.
    if (typeof el.selectionStart !== 'number' || typeof el.selectionEnd !== 'number') return false;
    if (el.selectionStart !== el.selectionEnd) return false; // active selection: this press collapses it first, same as native behavior
    return delta < 0 ? el.selectionStart === 0 : el.selectionEnd === el.value.length;
}

// Initialise the roving tabindex so only the first item is in the tab order.
// Called when the toolbar mounts; safe to call repeatedly.
export function initToolbarRoving(toolbarId) {
    const container = document.getElementById(toolbarId);
    const items = getToolbarItems(toolbarId);
    if (items.length === 0) {
        // No focusable items (yet, or any more — e.g. the previously-active
        // item just became disabled and getToolbarItems() correctly dropped
        // it, see the comment there) — the container itself is the only
        // possible tab stop, so keep/restore its own tabindex="0" as a
        // fallback (matches the markup's initial state before hydration/
        // first render, and self-heals the disabled-active-item case on the
        // very next render since this runs unconditionally every render).
        restoreToolbarContainerFallback(toolbarId);
        return;
    }
    // Items exist: they now own the single roving tab stop. The container's own
    // tabindex="0" (set unconditionally in the Razor markup) must be retracted,
    // otherwise it stays a SECOND tab stop alongside the active item — Tab would
    // land on the container, then Tab again to the first item, breaking the
    // "exactly one tab stop" APG toolbar model this function exists to provide.
    if (container) container.setAttribute('tabindex', '-1');
    // If one item already holds focus, keep it; otherwise make the first the stop.
    const focusedIndex = items.findIndex(el => el === document.activeElement);
    applyToolbarRovingTabindex(items, focusedIndex >= 0 ? focusedIndex : 0);
}

// Move focus `delta` items from the currently focused item (clamped, no wrap —
// matches Radix RovingFocus default). Returns the new index, or -1.
export function moveToolbarFocus(toolbarId, delta) {
    const items = getToolbarItems(toolbarId);
    if (items.length === 0) {
        // Defense in depth for the interaction path (PR #356 round-6, Codex
        // P2): normally initToolbarRoving's own empty-fallback already fires
        // on the render that dropped the last item, but restoring it here too
        // means an arrow press that lands in this state — e.g. a mutation the
        // Blazor render cycle hasn't caught up with yet — still can't leave
        // the toolbar with zero tab stops.
        restoreToolbarContainerFallback(toolbarId);
        return -1;
    }
    const active = document.activeElement;
    // Defer to the caret UNLESS it is already at the boundary the press is
    // heading toward — see isAtEditableBoundary for the full contract. Without
    // the boundary check every item after an editable toolbar child was
    // permanently unreachable by keyboard (PR #356 round-5, Codex P2).
    if (isEditableFocusTarget(active) && !isAtEditableBoundary(active, delta)) return -1;
    let current = items.findIndex(el => el === document.activeElement);
    if (current < 0) current = 0;
    let next = current + delta;
    next = Math.max(0, Math.min(next, items.length - 1));
    applyToolbarRovingTabindex(items, next);
    items[next].focus();
    return next;
}

// Focus the first (last=false) or last (last=true) toolbar item — Home/End.
// Deliberately UNCHANGED by the round-5 boundary fix above: Home/End inside a
// text field is a well-established, unambiguous native meaning (jump within
// the field's own text, not the widget), so it stays caret-only here — arrows
// are the one pair the round-5 fix needed to make escape-capable, since
// "already at the edge" only has meaning for a directional key.
export function focusToolbarEdge(toolbarId, last) {
    const items = getToolbarItems(toolbarId);
    if (items.length === 0) {
        // Mirrors moveToolbarFocus's defense-in-depth restore above (PR #356
        // round-6, Codex P2).
        restoreToolbarContainerFallback(toolbarId);
        return -1;
    }
    if (isEditableFocusTarget(document.activeElement)) return -1; // Home/End move the caret instead
    const index = last ? items.length - 1 : 0;
    applyToolbarRovingTabindex(items, index);
    items[index].focus();
    return index;
}

// Type-to-focus (Radix menu typeahead). Finds the first enabled menu item whose
// trimmed text content starts with `query` (case-insensitive), scanning AFTER
// `currentIndex` first so a repeated keystroke cycles through same-prefix items,
// then wrapping to the start. Focuses + scrolls the match into view and returns
// its index, or -1 when nothing matches (the caller keeps the current focus).
// The query buffer + reset timing live in C# (shared MenuTypeahead helper); this
// function only does the DOM text match so it stays SSR-free and reusable across
// DropdownMenu / Menubar / MegaMenu.
export function focusMenuItemByTypeahead(containerId, query, currentIndex) {
    const items = getMenuItems(containerId);
    if (items.length === 0 || !query) return -1;
    const q = query.toLowerCase();

    const matches = (el) => (el.textContent || '').trim().toLowerCase().startsWith(q);

    // Single-char buffer: start one past the current item so the same letter
    // advances to the next candidate. Multi-char buffer: start at the current
    // item so "se" can still match the item the user is already on.
    const start = (currentIndex >= 0 && currentIndex < items.length)
        ? (query.length === 1 ? currentIndex + 1 : currentIndex)
        : 0;

    for (let i = 0; i < items.length; i++) {
        const idx = (start + i) % items.length;
        if (matches(items[idx])) {
            items[idx].focus();
            if (typeof items[idx].scrollIntoView === 'function') {
                items[idx].scrollIntoView({ block: 'nearest', inline: 'nearest' });
            }
            return idx;
        }
    }
    return -1;
}

// --- OTP Paste ---

const otpPasteHandlers = new Map();

export function registerOtpPaste(baseId, length, dotnetRef) {
    // Clean up any previous handlers for this baseId
    const existing = otpPasteHandlers.get(baseId);
    if (existing) {
        for (let i = 0; i < existing.length; i++) {
            const el = document.getElementById(`${baseId}-${i}`);
            if (el) el.removeEventListener('paste', existing[i]);
        }
    }

    const handlers = [];
    for (let i = 0; i < length; i++) {
        const el = document.getElementById(`${baseId}-${i}`);
        if (el) {
            const handler = (e) => {
                e.preventDefault();
                const text = (e.clipboardData || window.clipboardData).getData('text');
                // Forward raw text (sanity-capped) — the C# side filters per
                // InputMode. Stripping \D here destroyed alphanumeric codes
                // before OtpInput.FilterInput ever saw them.
                dotnetRef.invokeMethodAsync('OnOtpPaste', baseId, text.slice(0, 64));
            };
            el.addEventListener('paste', handler);
            handlers.push(handler);
        } else {
            handlers.push(null);
        }
    }
    otpPasteHandlers.set(baseId, handlers);
}

export function unregisterOtpPaste(baseId, length) {
    const handlers = otpPasteHandlers.get(baseId);
    if (handlers) {
        for (let i = 0; i < handlers.length; i++) {
            if (handlers[i]) {
                const el = document.getElementById(`${baseId}-${i}`);
                if (el) el.removeEventListener('paste', handlers[i]);
            }
        }
        otpPasteHandlers.delete(baseId);
    }
}

// --- DataGrid cross-engine drag arbiter ---
// Column resize, column reorder, and row reorder are three independent
// pointer-driven engines, each guarding itself against a SECOND pointer on
// its OWN affordance (Codex round-5: resize's local `isDragging`, column/row
// reorder's local `drag`). None of those local guards can see each other, so
// a second touch that lands on a DIFFERENT engine's affordance — e.g. a
// finger taps a resize handle while a column (or row) reorder drag is
// already live on the same grid — still passes its own engine's gate and
// starts a competing gesture: both then fight over the same global
// `document.body.style.cursor`/`userSelect`, and whichever gesture's
// pointerId gets orphaned by the other engine's DOM mutations never receives
// its matching pointerup/cancel and is stranded mid-drag.
// One token per grid instance — claimed atomically at the same point each
// engine already commits to starting a drag, released the moment that drag
// ends/cancels/never-arms — closes every cross-engine combination (resize
// blocks reorder, reorder blocks resize, column reorder blocks row reorder
// and back) while leaving independent grids on the same page free to drag
// concurrently. Column/row reorder additionally hold the token through their
// whole post-release settle window (round-10 #3): the drop itself only
// SCHEDULES the .NET commit after a ~180ms glide, so releasing at drop time
// would let a new gesture claim the grid while that queued commit is still
// pending — the token is released only once the settle timeout actually
// fires (or is torn down by cancelActiveDrag), not when the pointer is
// released.

const gridActiveDrags = new Map(); // gridId -> engine name currently owning the live drag

function claimGridDrag(gridId, engine) {
    if (!gridId) return true; // couldn't resolve an owning grid — fail open, nothing to arbitrate against
    const owner = gridActiveDrags.get(gridId);
    // ANY existing owner means the grid is busy — including the SAME engine
    // name. Each resize handle/reorder registration keeps its own per-instance
    // `isDragging`/`drag` state, so a second pointer landing on a DIFFERENT
    // instance of the SAME engine (e.g. a second column's resize handle while
    // the first column's resize is still live) reaches this arbiter without
    // ever tripping that other instance's local guard. Comparing owner to the
    // engine name only caught cross-engine collisions; same-engine concurrent
    // drags slipped through because owner === engine looked like a no-op
    // re-claim instead of a second, competing drag (round-7 #1).
    if (owner) return false;
    gridActiveDrags.set(gridId, engine);
    return true;
}
function releaseGridDrag(gridId, engine) {
    if (!gridId) return;
    if (gridActiveDrags.get(gridId) === engine) gridActiveDrags.delete(gridId);
}

// --- DataGrid Column Resize ---

const columnResizeHandlers = new Map();

// A single reusable guideline element that tracks the resized column's active
// edge (a 2px primary line spanning the table's vertical extent). It's a purely
// visual affordance drawn in JS during the drag — never re-rendered by Blazor —
// so it costs nothing on the per-move hot path beyond one style write per frame.
let resizeGuideline = null;
// Draw the guideline on the resized column's ACTUAL edge — not the pointer.
// The dragged edge is the column's inline-end: visual RIGHT in LTR, visual LEFT
// in RTL (the handle + its ::before divider sit on inline-end in both). We read
// that edge from the header cell's own getBoundingClientRect, so the line lands
// exactly on the visible divider in every frame. Tracking the raw pointer
// (xClient) drifted for three reasons that all vanish here: the grab point sits
// a few px inside the 12px handle (constant offset), the pointer overruns the
// edge once the width clamps at min/max (unbounded offset), and under
// table-layout:auto the edge doesn't move 1:1 with the pointer. Because the
// guideline is position:fixed (viewport frame) and getBoundingClientRect is also
// viewport-relative, the same coordinate holds under horizontal scroll and with
// pinned (sticky) columns with no extra math. `rtl` is passed in (captured once
// per drag) so there's no getComputedStyle on the per-frame hot path.
function showResizeGuideline(th, rtl) {
    const table = th.closest('table');
    const tableRect = (table || th).getBoundingClientRect();
    const thRect = th.getBoundingClientRect();
    const edgeX = rtl ? thRect.left : thRect.right;
    if (!resizeGuideline) {
        resizeGuideline = document.createElement('div');
        resizeGuideline.setAttribute('data-slot', 'datagrid-resize-guideline');
        const s = resizeGuideline.style;
        s.position = 'fixed';
        s.width = '2px';
        s.background = 'var(--color-primary, #6366f1)';
        s.opacity = '0.85';
        s.pointerEvents = 'none';
        s.zIndex = '60';
        s.borderRadius = '1px';
        s.boxShadow = '0 0 6px color-mix(in oklab, var(--color-primary, #6366f1) 60%, transparent)';
        document.body.appendChild(resizeGuideline);
    }
    const s = resizeGuideline.style;
    s.display = 'block';
    s.top = tableRect.top + 'px';
    s.height = tableRect.height + 'px';
    // Centre the 2px line on the edge (left = edge - half-width).
    s.left = (edgeX - 1) + 'px';
}
function hideResizeGuideline() {
    if (resizeGuideline) resizeGuideline.style.display = 'none';
}

export function registerColumnResize(handleId, dotnetRef, minWidth, maxWidth) {
    const handle = document.getElementById(handleId);
    if (!handle) return;
    const th = handle.closest('th');
    if (!th) return;
    // Resolved once at registration — the arbiter token this handle's drags
    // claim/release against (see the cross-engine arbiter above). null when
    // the handle isn't (yet) inside a DataGrid root, which just disables
    // arbitration for it rather than failing registration.
    const gridEl = th.closest('[data-grid-id]');
    const gridId = gridEl ? gridEl.getAttribute('data-grid-id') : null;

    let startX = 0;
    let startWidth = 0;
    let currentWidth = 0;
    let isDragging = false;
    let colBodyCells = [];
    // Horizontal writing direction: in RTL the resize handle sits on the visual
    // LEFT (inline-end), so a rightward pointer move must SHRINK the column.
    // Captured per pointerdown from the live computed style so a runtime dir
    // flip (e.g. locale switch) is honoured without re-registering.
    let dirMultiplier = 1;
    // rAF throttle: coalesce successive pointermoves into one DOM mutation
    // per frame. Without this, a high-frequency pointer (120-240 Hz on modern
    // touch/trackpad) triggers a full table reflow per event — visible as
    // micro-stuttering on wide grids. One write per frame matches the
    // refresh rate and lets the browser batch layout/paint naturally.
    let rafId = 0;
    let pendingWidth = 0;

    const min = Math.max(1, Number(minWidth) || 50);
    const max = Number(maxWidth) > 0 ? Number(maxWidth) : Number.POSITIVE_INFINITY;

    // Apply the width to the header + every body cell in the same column — using
    // direct style writes so we can drag smoothly without a Blazor round-trip on
    // every mouse move. We commit the final width ONCE on mouseup.
    const applyWidth = (w) => {
        const wpx = w + 'px';
        th.style.width = wpx;
        th.style.minWidth = wpx;
        for (const cell of colBodyCells) {
            cell.style.width = wpx;
            cell.style.minWidth = wpx;
        }
    };

    const gatherBodyCells = () => {
        const table = th.closest('table');
        if (!table) return [];
        const headerRow = th.parentElement;
        if (!headerRow) return [];
        const colIndex = Array.prototype.indexOf.call(headerRow.children, th);
        if (colIndex < 0) return [];
        const tbody = table.querySelector('tbody');
        if (!tbody) return [];
        const cells = [];
        for (const row of tbody.rows) {
            const cell = row.children[colIndex];
            // Skip group/detail rows that span the whole table with a single
            // colspan cell — writing a fixed width onto them would fight the
            // colspan and jam the layout.
            if (cell && !(cell.colSpan && cell.colSpan > 1)) cells.push(cell);
        }
        return cells;
    };

    let activePointerId = null;

    // Migrated mouse* → pointer* (rc.43 mobile audit). Pointer events are a
    // superset that handles mouse, touch, and pen with a single API. We bind
    // pointermove/pointerup on the HANDLE itself (not document) because
    // setPointerCapture redirects subsequent pointer events for that pointerId
    // to the capturing element — drag stays alive even when the finger/cursor
    // strays outside the column header.
    const onPointerDown = (e) => {
        // Ignore non-primary mouse buttons; touch/pen always fall through.
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        // A second pointerdown while a resize is already live (second touch
        // point, concurrent pen) must not overwrite the single activePointerId/
        // startWidth/startX state — the first drag's own pointermove/pointerup
        // would silently stop matching activePointerId and get stranded mid-drag
        // (Codex round-5 #3, mirrored onto resize).
        if (isDragging) return;
        // Cross-engine: a column/row reorder already live on this grid owns
        // the shared arbiter token — refuse to start a competing resize
        // (round-6 finding; see the arbiter comment above registerColumnResize).
        if (!claimGridDrag(gridId, 'resize')) return;
        isDragging = true;
        activePointerId = e.pointerId;
        startX = e.clientX;
        startWidth = th.getBoundingClientRect().width;
        currentWidth = startWidth;
        pendingWidth = startWidth;
        dirMultiplier = getComputedStyle(th).direction === 'rtl' ? -1 : 1;
        colBodyCells = gatherBodyCells();
        document.body.style.cursor = 'col-resize';
        document.documentElement.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        // Active affordance: the CSS-authored ::before divider turns primary/2px
        // while data-resizing is set (survives the pointer straying off the
        // handle, which :active would not once capture kicks in).
        handle.dataset.resizing = 'true';
        showResizeGuideline(th, dirMultiplier === -1);
        try { handle.setPointerCapture(e.pointerId); } catch (_) { }
        // preventDefault on pointerdown stops touch from initiating a page
        // scroll/zoom gesture before the move begins.
        e.preventDefault();
        e.stopPropagation();
    };
    const flushPendingWidth = () => {
        rafId = 0;
        if (!isDragging) return;
        currentWidth = pendingWidth;
        applyWidth(pendingWidth);
        showResizeGuideline(th, dirMultiplier === -1);
    };
    const onPointerMove = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        const delta = (e.clientX - startX) * dirMultiplier;
        let w = startWidth + delta;
        if (w < min) w = min;
        else if (w > max) w = max;
        if (w === pendingWidth) {
            if (!rafId) rafId = requestAnimationFrame(flushPendingWidth);
            if (e.cancelable) e.preventDefault();
            return;
        }
        pendingWidth = w;
        if (!rafId) rafId = requestAnimationFrame(flushPendingWidth);
        // Block scroll on touch devices while actively dragging.
        if (e.cancelable) e.preventDefault();
    };
    // `keepToken` lets a caller that's about to fire a commit interop hold the
    // arbiter through that commit's completion instead of releasing here —
    // round-12 #2: releasing right after STARTING invokeMethodAsync (rather
    // than once it actually settles) let another gesture claim the grid while
    // a slow .NET OnColumnResizeCommit handler was still in flight, reopening
    // the exact race the settle-window token hold already closes for reorder.
    const endDrag = (keepToken) => {
        isDragging = false;
        if (!keepToken) releaseGridDrag(gridId, 'resize');
        if (rafId) { cancelAnimationFrame(rafId); rafId = 0; }
        document.body.style.cursor = '';
        document.documentElement.style.cursor = '';
        document.body.style.userSelect = '';
        delete handle.dataset.resizing;
        hideResizeGuideline();
    };
    const onPointerUp = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        // Apply any pending frame synchronously so the committed width
        // matches what the user released on (no half-frame visual snap).
        if (pendingWidth !== currentWidth) {
            currentWidth = pendingWidth;
            applyWidth(pendingWidth);
        }
        // No actual width change occurred — a plain click/tap, or critically
        // the first pointerdown/pointerup pair the browser still sends before
        // dblclick fires. Skip the commit so consumers of OnColumnResizeCommit
        // don't see a spurious no-op resize event on every auto-fit gesture;
        // onDoubleClick fires its own commit for the real auto-fit width
        // (Codex round-5 #4).
        const willCommit = currentWidth !== startWidth;
        endDrag(willCommit);
        try { handle.releasePointerCapture(e.pointerId); } catch (_) { }
        activePointerId = null;
        if (!willCommit) return;
        // Hold the token until the interop promise actually settles (round-12
        // #2) — `finally` releases on both a successful commit AND a rejected
        // one, so a failed .NET round-trip can never strand the token.
        dotnetRef.invokeMethodAsync('OnColumnResizeCommit', handleId, currentWidth, false)
            .finally(() => releaseGridDrag(gridId, 'resize'));
    };
    const onPointerCancel = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        cancelActiveDrag();
        // No commit on cancel — the user aborted (e.g. system gesture took over).
    };
    // Aborts an in-flight drag without committing — shared by pointercancel and
    // unregisterColumnResize (unmount mid-drag: route change, column hidden,
    // Resizable toggled off). Restores the pre-drag width so the DOM doesn't
    // drift from the (uncommitted) grid state, and clears every piece of global
    // state a live drag leaves behind (cursor, selection, guideline, capture) —
    // mirrors registerColumnReorder's cancelActiveDrag.
    const cancelActiveDrag = () => {
        if (!isDragging) return;
        if (currentWidth !== startWidth) applyWidth(startWidth);
        currentWidth = startWidth;
        pendingWidth = startWidth;
        const pid = activePointerId;
        endDrag();
        if (pid !== null) {
            try { handle.releasePointerCapture(pid); } catch (_) { }
        }
        activePointerId = null;
    };
    // Measures a cell's truly intrinsic content width — off-DOM, so a full-width
    // table's auto-layout extra-space distribution (which inflates a live
    // width:auto cell to fill the remaining table width) can't skew the result.
    // A deep clone is appended to <body> as an isolated single-cell box (browsers
    // wrap an orphan table-cell in an anonymous 1x1 table), measured, then removed
    // — the real table's live cells/layout are never touched.
    const measureIntrinsicWidth = (cell) => {
        const probe = cell.cloneNode(true);
        const ps = probe.style;
        ps.position = 'absolute';
        ps.visibility = 'hidden';
        ps.left = '-9999px';
        ps.top = '-9999px';
        ps.width = 'auto';
        ps.minWidth = '0';
        ps.maxWidth = 'none';
        ps.whiteSpace = 'nowrap';
        document.body.appendChild(probe);
        const w = probe.getBoundingClientRect().width;
        probe.remove();
        return w;
    };
    // Double-click → auto-fit the column to its widest content (header + body).
    const onDoubleClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        // Cross-engine: a reorder (or another resize) already live on this
        // grid owns the shared arbiter token — auto-fit was the one gesture
        // that never checked, so it could mutate widths + dispatch
        // OnColumnResizeCommit while a reorder settle window still held the
        // token (round-11 #2). Refuse silently exactly like onPointerDown —
        // no queueing. Auto-fit is instantaneous (no drag of its own to hold
        // the token through), so it's claimed here and held through the
        // commit interop's completion (round-12 #2) rather than released
        // synchronously right after invokeMethodAsync starts — a slow .NET
        // commit could otherwise let another gesture claim the grid mid-commit.
        if (!claimGridDrag(gridId, 'resize')) return;
        const cells = [th, ...gatherBodyCells()];
        let natural = 0;
        for (const c of cells) natural = Math.max(natural, measureIntrinsicWidth(c));
        natural = Math.ceil(natural) + 2; // hairline breathing room
        let w = natural;
        if (w < min) w = min; else if (w > max) w = max;
        colBodyCells = gatherBodyCells();
        applyWidth(w);
        currentWidth = w;
        // `finally` releases on both a successful commit and a rejected one,
        // so a failed .NET round-trip can never strand the token.
        dotnetRef.invokeMethodAsync('OnColumnResizeCommit', handleId, w, true)
            .finally(() => releaseGridDrag(gridId, 'resize'));
    };
    handle.addEventListener('pointerdown', onPointerDown);
    // passive: false so preventDefault works inside pointermove on touch.
    handle.addEventListener('pointermove', onPointerMove, { passive: false });
    handle.addEventListener('pointerup', onPointerUp);
    handle.addEventListener('pointercancel', onPointerCancel);
    handle.addEventListener('dblclick', onDoubleClick);
    columnResizeHandlers.set(handleId, {
        handle, th, min, max, dotnetRef, applyWidth, gatherBodyCells, gridId,
        onPointerDown, onPointerMove, onPointerUp, onPointerCancel, onDoubleClick,
        cancelActiveDrag,
    });
}

// Keyboard resize: nudge the column width by `delta` px (sign already carries
// grow/shrink intent from the caller; we still flip for RTL so ArrowRight always
// means "toward the visual right"). Reuses the registered min/max + commit path,
// so a keyboard resize persists exactly like a pointer drag. Discrete (one call
// per keypress) so a .NET round-trip here is well within the hot-path law.
export function nudgeColumnResize(handleId, delta) {
    const h = columnResizeHandlers.get(handleId);
    if (!h) return 0;
    const dir = getComputedStyle(h.th).direction === 'rtl' ? -1 : 1;
    const current = h.th.getBoundingClientRect().width;
    let w = current + Number(delta) * dir;
    if (w < h.min) w = h.min; else if (w > h.max) w = h.max;
    w = Math.round(w);
    // Already at Min/MaxWidth and the nudge is further into the clamp: the
    // computed width is identical to what's already committed (rounding the
    // live rect can itself introduce ±1px drift from the last committed
    // value, so compare against the rounded CURRENT width, not w === current
    // pre-round). Skip the write/commit entirely — mirrors the pointer
    // engine's motionless-drag no-op (round-5 #4) — so a repeated
    // Ctrl+ArrowLeft/Right at the clamp doesn't keep firing
    // OnColumnResizeCommit/autosave/re-render for a no-op keypress
    // (round-6 finding).
    if (w === Math.round(current)) return w;
    // Cross-engine: a pointer/dblclick resize, or a column/row reorder still
    // in its post-release settle window, already owns this grid's shared
    // arbiter token — refuse this discrete keyboard nudge silently, exactly
    // like onPointerDown/onDoubleClick do (round-12 #3). No queueing: the
    // width is left untouched and the next keypress after the token frees up
    // simply tries again.
    if (!claimGridDrag(h.gridId, 'resize')) return Math.round(current);
    // Refresh the body-cell list (rows may have paged/virtualized since register).
    const cells = h.gatherBodyCells();
    const wpx = w + 'px';
    h.th.style.width = wpx; h.th.style.minWidth = wpx;
    for (const c of cells) { c.style.width = wpx; c.style.minWidth = wpx; }
    // Held through the commit interop's completion, not released
    // synchronously right after invokeMethodAsync starts — `finally` covers
    // a rejected commit too, mirroring every other commit site (round-12 #2).
    h.dotnetRef.invokeMethodAsync('OnColumnResizeCommit', handleId, w, false)
        .finally(() => releaseGridDrag(h.gridId, 'resize'));
    return w;
}

export function unregisterColumnResize(handleId) {
    const h = columnResizeHandlers.get(handleId);
    if (h) {
        // Unmounting a resizable header mid-drag (route change, column hidden,
        // Resizable toggled off) must not just drop the listeners — the active
        // drag is aborted first so the global cursor/selection styles, pointer
        // capture, and resize guideline never outlive the handle they belong to.
        h.cancelActiveDrag();
        const el = h.handle || document.getElementById(handleId);
        if (el) {
            el.removeEventListener('pointerdown', h.onPointerDown);
            el.removeEventListener('pointermove', h.onPointerMove);
            el.removeEventListener('pointerup', h.onPointerUp);
            el.removeEventListener('pointercancel', h.onPointerCancel);
            el.removeEventListener('dblclick', h.onDoubleClick);
        }
        columnResizeHandlers.delete(handleId);
    }
}

// --- DataGrid Column Reorder FLIP Animation ---
//
// FLIP (First-Last-Invert-Play): the technique for animating layout changes
// that the browser can't smooth on its own. Blazor rerenders the table after
// a reorder and the columns just snap to their new positions; with this in
// place, the consumer can capture the old positions BEFORE the rerender and
// animate from old → new AFTER, so users see neighbour columns smoothly slide
// into place rather than the destination just appearing.
//
// Stable identity for each column is the `data-col-id` attribute on the <th>
// (the DOM `id` is index-based and changes on reorder, so it's unusable here).
// We also pick up body cells in the same visual column by their index in the
// header row at capture time, so they tag along with the header's animation.

const columnReorderSnapshots = new Map(); // gridId -> { colId: { left, cells: [el,...] } }
const columnReorderInFlight = new Map();  // gridId -> Set<HTMLElement> (cells with inline FLIP styles)
// gridId -> Set<HTMLElement> holding a JS-authored settle transform for a
// commit .NET hasn't resolved yet — element REFERENCES captured at the exact
// moment finishDrag applies them, so ClearColumnReorderTransforms can find
// and strip them even if a rerender (grouping/virtualization/pin-partition
// change racing the settle window) strips data-col-id from these same nodes
// before the reject path runs (round-11 #1). Drained by captureColumnRects
// (accept) and clearColumnReorderTransforms (reject).
const columnReorderSettleEls = new Map();

// A DataGrid nested inside another reorderable grid's DetailTemplate/cell
// template sits inside the outer grid's DOM subtree too, so a plain
// querySelectorAll from the outer `grid` root also returns the inner grid's
// headers/rows. Every candidate collected that way must be re-checked
// against its OWN closest grid root before being treated as belonging to
// `grid` — shared by both the column and row reorder/FLIP engines below
// (Codex round-4 #1/#2).
const ownedByGrid = (nodeList, grid) =>
    Array.prototype.filter.call(nodeList, (el) => el.closest('[data-grid-id]') === grid);

// Inline style properties the column/row pointer-drag engines temporarily
// overwrite while a drag is armed (opacity/position/zIndex/pointerEvents/
// boxShadow — the last one added for the column-header lift affordance, see
// registerColumnReorder's armDrag). Captured before the overwrite and
// restored verbatim on cleanup instead of being blanked to '' — a consumer's
// own ColumnStyle/RowStyle inline declaration on one of these properties
// would otherwise be permanently erased, since Blazor may not rewrite an
// unchanged style attribute on the next render (Codex round-4 #4).
const REORDER_DRAG_STYLE_PROPS = ['opacity', 'position', 'zIndex', 'pointerEvents', 'boxShadow'];
const captureDragStyles = (el) => {
    const saved = {};
    for (const prop of REORDER_DRAG_STYLE_PROPS) saved[prop] = el.style[prop];
    return saved;
};
const restoreDragStyles = (el, saved) => {
    for (const prop of REORDER_DRAG_STYLE_PROPS) el.style[prop] = saved[prop];
};

function clearFlipStyles(cell) {
    cell.style.transition = '';
    cell.style.transform = '';
    cell.style.willChange = '';
}

export function captureColumnRects(gridId) {
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    // If a previous FLIP for this grid is still mid-flight, force its
    // cells back to identity BEFORE measuring — otherwise the rects we
    // capture include the in-flight translateX and the next animation
    // starts from a wrong "First". Reorders triggered faster than the
    // animation duration are uncommon but possible (rapid drag).
    const inFlight = columnReorderInFlight.get(gridId);
    if (inFlight) {
        for (const cell of inFlight) clearFlipStyles(cell);
        columnReorderInFlight.delete(gridId);
    }
    // This capture is the accept path settling successfully — the settle-els
    // reference set from finishDrag is now stale (its transforms are about to
    // be re-measured/cleared below by attribute query, which still works here
    // since an accepted commit's rerender keeps data-col-id). Drop it so a
    // later drag's reject cleanup never acts on this drag's old elements.
    columnReorderSettleEls.delete(gridId);
    const headers = ownedByGrid(grid.querySelectorAll('th[data-col-id]'), grid);
    if (!headers.length) return;
    const headerRow = headers[0].parentElement;
    const tbody = grid.querySelector('table tbody');
    const snapshot = {};
    headers.forEach((th) => {
        const colId = th.dataset.colId;
        if (!colId) return;
        const colIdx = Array.prototype.indexOf.call(headerRow.children, th);
        const cells = [th];
        if (tbody && colIdx >= 0) {
            for (const row of tbody.rows) {
                const cell = row.children[colIdx];
                if (!cell) continue;
                // Skip non-data rows: grouped grids render DataGridGroupRow
                // as a single <td colspan="N"> that spans every column.
                // Capturing it under the reorder-column index would later
                // FLIP-translate the entire group header sideways when only
                // a single data column moved — visible jitter (Codex
                // review on #48). colSpan > 1 is the cheapest way to
                // identify span cells; data cells always have colSpan 1.
                if (cell.colSpan && cell.colSpan > 1) continue;
                cells.push(cell);
            }
        }
        // Measure BEFORE clearing: getBoundingClientRect() reflects any inline
        // transform still applied by the pointer-based reorder drag (JS leaves the
        // dragged column's + live-shifted siblings' transforms in place through the
        // commit — see registerColumnReorder's finishDrag — so this "left" is the
        // accurate settled preview position, not a stale pre-drag one). Clearing
        // right after the read hands the DOM back clean for the mutation this
        // capture precedes and for AnimateColumnReorder's post-render remeasure.
        // ONLY the FLIP engine's own properties (transform/transition/willChange)
        // — never opacity/position/zIndex/pointerEvents. This capture pass runs
        // for EVERY header+body cell in the grid on every reorder, including
        // columns the drag never touched at all, so blanking those four here
        // would permanently erase a consumer's own ColumnStyle declaration on
        // any untouched column the first time ANY column in the grid is
        // reordered (Blazor skips rewriting an unchanged style attribute on the
        // next render) — the same failure mode as Codex round-4 #4, just in the
        // FLIP-capture path instead of armDrag/finishDrag. Mirrors
        // captureRowRects's clearRowFlipStyles(tr) below.
        const left = th.getBoundingClientRect().left;
        for (const cell of cells) clearFlipStyles(cell);
        snapshot[colId] = { left, cells };
    });
    columnReorderSnapshots.set(gridId, snapshot);
}

// Snaps every header + body cell back to identity when a delayed column-reorder
// commit is REJECTED client-side (round-8 #4) — e.g. the target column was
// hidden/removed or moved into another pin partition during the settle window,
// so ReorderColumnByIdAsync never reaches HandleColumnReorder's captureColumnRects
// call. registerColumnReorder's finishDrag already left the dragged column and
// every live-shifted sibling cell translated for that call to clear as part of
// the FLIP handoff; on rejection nothing else will, so this is the dedicated
// no-animation counterpart. No transition is (re)applied — finishDrag's own
// settle glide already finished visually before the (now-rejected) commit was
// even dispatched, so replaying a second glide here would misleadingly look
// like an accepted move.
export function clearColumnReorderTransforms(gridId) {
    // Element-reference cleanup FIRST (round-11 #1): a rerender racing this
    // reject can strip data-col-id from the very cells finishDrag left
    // transformed, before the attribute-based sweep below ever runs — these
    // are the same DOM nodes finishDrag touched, found by reference instead
    // of by an attribute the rerender may already have removed.
    const settleEls = columnReorderSettleEls.get(gridId);
    if (settleEls) {
        for (const cell of settleEls) clearFlipStyles(cell);
        columnReorderSettleEls.delete(gridId);
    }
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    const inFlight = columnReorderInFlight.get(gridId);
    if (inFlight) {
        for (const cell of inFlight) clearFlipStyles(cell);
        columnReorderInFlight.delete(gridId);
    }
    columnReorderSnapshots.delete(gridId);
    const headers = ownedByGrid(grid.querySelectorAll('th[data-col-id]'), grid);
    if (!headers.length) return;
    const headerRow = headers[0].parentElement;
    const tbody = grid.querySelector('table tbody');
    headers.forEach((th) => {
        clearFlipStyles(th);
        const colIdx = Array.prototype.indexOf.call(headerRow.children, th);
        if (!tbody || colIdx < 0) return;
        for (const row of tbody.rows) {
            const cell = row.children[colIdx];
            if (cell && !(cell.colSpan > 1)) clearFlipStyles(cell);
        }
    });
}

export function animateColumnReorder(gridId, durationMs) {
    const snapshot = columnReorderSnapshots.get(gridId);
    columnReorderSnapshots.delete(gridId);
    if (!snapshot) return;
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    const duration = Number(durationMs) > 0 ? Number(durationMs) : 200;

    const headers = ownedByGrid(grid.querySelectorAll('th[data-col-id]'), grid);
    const inFlight = new Set();
    headers.forEach((th) => {
        const colId = th.dataset.colId;
        const snap = snapshot[colId];
        if (!snap) return;
        const newLeft = th.getBoundingClientRect().left;
        const delta = snap.left - newLeft;
        if (Math.abs(delta) < 1) return;

        // Inverse-translate header + cached body cells to their previous
        // visual position, then animate transform back to 0 on the next
        // frame. The cells array was captured at snapshot time, so it
        // refers to the SAME DOM nodes — the column's body cells move with
        // their header even though the DOM order under tbody changed.
        for (const cell of snap.cells) {
            cell.style.transition = 'none';
            cell.style.transform = `translateX(${delta}px)`;
            cell.style.willChange = 'transform';
            inFlight.add(cell);
        }
        // Force layout flush so the inverse transform is registered before
        // we kick off the transition. Reading offsetWidth from any one cell
        // is enough — it's a synchronous reflow trigger.
        // eslint-disable-next-line no-unused-expressions
        th.offsetWidth;
        for (const cell of snap.cells) {
            cell.style.transition = `transform ${duration}ms cubic-bezier(0.22, 1, 0.36, 1)`;
            cell.style.transform = '';
        }
    });
    if (inFlight.size === 0) return;
    columnReorderInFlight.set(gridId, inFlight);
    // Clear inline styles after the transition so they don't leak into
    // the next reorder. setTimeout (not transitionend) keeps the cleanup
    // O(1) listeners regardless of cell count.
    window.setTimeout(() => {
        // Only clear cells we still own — captureColumnRects may have
        // pre-emptively cleared them already if a new reorder kicked in.
        const owned = columnReorderInFlight.get(gridId);
        if (owned !== inFlight) return;
        for (const cell of inFlight) clearFlipStyles(cell);
        columnReorderInFlight.delete(gridId);
    }, duration + 50);
}

// --- DataGrid Column Reorder + Drag-to-Group: ONE unified pointer engine ---
// (mouse + touch + pen — rc.42 folded drag-to-group into this same engine)
//
// ReUI parity (keenthemes/reui data-grid-table-dnd.tsx): ONE drag path drives
// every pointer type AND both drop semantics. No ghost image, no drop-
// indicator line — the dragged column (header + its body cells) translates IN
// PLACE following the pointer at opacity 0.8 / z above siblings, while
// same-pin siblings LIVE-SHIFT aside (transform-only, ~220ms ease) to
// continuously preview the final order as the pointer crosses their
// midpoints. Native HTML5 drag-and-drop is no longer used at all — it used to
// own drag-to-group as a SEPARATE drag surface (a header with draggable=true
// firing its own dragstart), which is fundamentally incompatible with this
// engine: a native dragstart on an element under an active pointer sequence
// fires pointercancel, so the two could never coexist on the same header.
// Folding drag-to-group in here is what makes it work at all, and gives touch
// drag-to-group for free (native DnD never fired on touch in the first
// place).
//
// Initiation (unchanged):
//   * the dedicated grip (touch/pen/mouse) arms the drag immediately — it has
//     no other function, so there's nothing to disambiguate;
//   * the header itself ALSO arms it for MOUSE ONLY, but only past a small
//     movement threshold (REORDER_MOVE_THRESHOLD) so a plain click still
//     reaches the sort button / other header controls — touch/pen stay
//     grip-only (a tap must stay unambiguous with tap-to-sort).
//
// Mode-switching (new): once armed, drag.mode toggles between 'reorder' and
// 'panel' on every pointermove by hit-testing the pointer against the grid's
// group-panel element (cached rect — the panel doesn't move during a column
// drag, only the table body scrolls; see onPointerDown). 'panel' mode relaxes
// the live sibling-shift preview back to identity (the dragged header keeps
// following the pointer horizontally, but nothing else moves) and — only when
// the dragged column is itself Groupable (drag.groupable, from its
// data-groupable attribute) — lights up the panel's data-drop-target
// highlight. Leaving the panel (or dragging a non-groupable column over it)
// falls straight back into normal reorder projection with no special-casing.
//
// Every per-move computation (target index, sibling shift deltas, panel hit
// test) is cached at drag start and only recomputed when something actually
// changes — zero .NET calls and zero layout reads in the pointermove hot
// loop, matching the resize handle's perf law. Escape / pointercancel
// animates back to identity and commits nothing, in EITHER mode; release
// commits ONCE via OnColumnReorderCommit(gridId, srcId, tgtId) — 'reorder'
// mode passes the projected sibling's column id (or null, cancel);  'panel'
// mode passes GroupPanelDropTargetId when groupable (else null, cancel — a
// non-groupable column dropped on the panel is treated exactly like a
// release outside any valid target). DataGrid.razor's commit lambda branches
// on that sentinel to call AddGroupField instead of ReorderColumnByIdAsync —
// reusing the SAME DotNetObjectReference channel reorder already uses rather
// than inventing a parallel one. A normal 'reorder' commit still hands off to
// captureColumnRects (called right after, from HandleColumnReorder), which
// measures the settled preview as FLIP's "First" and clears the inline drag
// styles itself.

const columnReorderPointerHandlers = new Map(); // gridId -> { grid, onPointerDown, ... }

const REORDER_MOVE_THRESHOLD = 5;  // px — mouse header-wide init must clear this to arm
const REORDER_SETTLE_MS = 180;     // release/cancel glide duration
const REORDER_SHIFT_MS = 220;      // sibling live-shift ease duration
const REORDER_EASE = 'cubic-bezier(0.22, 1, 0.36, 1)';
// Header-drag redesign (zone model B: whole-surface sort click + title drag):
const REORDER_LONGPRESS_MS = 350;        // touch/pen header-wide (non-grip) press-and-hold before arming
const REORDER_LONGPRESS_CANCEL_PX = 10;  // touch/pen: movement past this BEFORE the timer fires cancels the pending arm (native scroll wins)
const REORDER_LIFT_MS = 120;             // header lift (scale+shadow) pop-in duration on arm
const REORDER_NUDGE_MS = 150;            // non-reorderable "can't drag this" nudge-and-spring duration
const REORDER_AUTOSCROLL_EDGE_PX = 48;   // distance from the scroll container's edge that triggers auto-scroll
const REORDER_AUTOSCROLL_MAX_PX = 18;    // px/frame at the very edge (accelerates linearly from 0 at the edge threshold)

// Unified drag-to-group (rc.42): sentinel "target id" sent to OnColumnReorderCommit
// when the drag was released over the group panel instead of another header — MUST
// match DataGrid.razor's GroupPanelDropTargetId exactly. Opaque to JS beyond that;
// never collides with a real column id (DataGridColumn.Id defaults to 8 hex chars).
const GROUP_PANEL_DROP_TARGET_ID = '__group-panel__';
// Attribute components.js toggles directly on the group panel element (no Blazor
// round-trip) while an armed drag hovers it — see lumeo.css for the highlight rule.
const GROUP_PANEL_DROP_ATTR = 'data-drop-target';

// Floating chip ghost (drag-to-group visual): restores the "carrying the column up
// to the panel" feel native drag-and-drop's browser-generated drag image used to
// give for free, lost when drag-to-group was unified into this pointer engine
// (rc.42, see the big comment block above). A real <th> can only translateX within
// its own row (applyLiveTranslate below); a translateY toward the group panel would
// be clipped by the table's own .overflow-auto wrapper, so nothing ever showed the
// column visually leaving the row on its way up to the panel — grouping still
// worked, it just felt like nothing was happening. One ghost element per drag
// (created in armDrag, torn down in finishDrag — see createGhostEl/teardownGhost
// inside registerColumnReorder), fixed-positioned and appended to document.body so
// it's clipped by nothing, styled with the group panel's OWN chip class string
// (DataGrid.razor's data-slot="datagrid-group-panel" chip div) so every theme token
// (bg-card, border-border/60, etc.) applies exactly like "the chip this drop will
// create." Hidden while the pointer is inside the header row's band — there the
// real header cells already carry the drag visual via applyLiveTranslate's own
// lift+shift — and shown, tracking the pointer 1:1 via a fixed transform (no layout
// reads in the hot loop), the moment the pointer travels far enough outside that
// band. See updateGhostCarry.
const GHOST_CHIP_CLASS = 'inline-flex items-center gap-1 rounded bg-card border border-border/60 px-2 py-0.5 text-xs shadow-lg';
// LumeoIcons.Group's own path data (src/Lumeo/Icons/LumeoIcons.g.cs) plus
// SvgGlyph.razor's stroke-icon wrapper attributes, hardcoded here — the ghost is a
// raw DOM node built by this file, not a Blazor-rendered component, so it can't
// reference either directly. Kept in exact sync with the real chip's own icon
// (DataGrid.razor's group-panel chip) so the ghost genuinely previews it.
const GHOST_ICON_SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="h-3 w-3 text-muted-foreground" aria-hidden="true"><path d="M3 7V5c0-1.1.9-2 2-2h2" /><path d="M17 3h2c1.1 0 2 .9 2 2v2" /><path d="M21 17v2c0 1.1-.9 2-2 2h-2" /><path d="M7 21H5c-1.1 0-2-.9-2-2v-2" /><rect width="7" height="5" x="7" y="7" rx="1" /><rect width="7" height="5" x="10" y="12" rx="1" /></svg>';
// Above LITERALLY everything, including a Dialog/Sheet/Drawer hosting this grid —
// see OverlayService.cs's BaseZIndex (50) and its stacked per-instance tiers, all of
// which a mid-drag ghost must clear regardless of how deep the grid is nested.
const GHOST_Z_INDEX = 2147483647;
const GHOST_BAND_SLOP_PX = 8;     // px outside the header row's rect before the ghost takes over from the real header
const GHOST_MOUSE_OFFSET = 12;    // px, both axes — cursor offset so the pointer never covers the ghost (mouse/pen)
const GHOST_TOUCH_OFFSET_Y = -24; // px — touch gets an upward-only offset so the finger doesn't cover it
const GHOST_EXIT_MS = 150;        // group-panel commit: ghost scale+fades "into" the real chip at the drop point
const GHOST_CANCEL_FADE_MS = 120; // every other end path (drop elsewhere / cancel / Escape): quick fade, no scale

export function registerColumnReorder(gridId, dotnetRef) {
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    if (columnReorderPointerHandlers.has(gridId)) unregisterColumnReorder(gridId);

    let drag = null; // active/pending drag descriptor or null
    // Cells still carrying an engine-applied transform during the delayed-commit
    // settle window (release/cancel happened, finishDrag's window.setTimeout for
    // the commit/glide-back hasn't fired yet). `drag` is already null by then, so
    // cancelActiveDrag alone can't reach them — unregister-during-settle (round-9
    // #1) needs this separate handle to cancel the pending timeout AND strip the
    // transforms, or the columns stay visually stuck with no future FLIP pass to
    // clean them up.
    let pendingSettle = null; // { timeoutId, cells: Set<HTMLElement> } or null
    // Non-reorderable column: a lightweight "attempted drag" watcher (NOT the shared
    // `drag` descriptor — never claims the grid arbiter, never preventDefaults) that
    // shows the tiny nudge-and-spring feedback if the pointer moves past the same
    // threshold a real drag would arm at. See onPointerDown/onPointerMove.
    let nudgeWatch = null; // { th, pointerId, startX, startY, firedNudge } or null
    // One-shot capture-phase click swallow armed by a header-wide (non-grip) drag the
    // moment it actually arms — see armSortClickSuppression's comment above its
    // definition for why this is needed at all.
    let pendingSortClickSuppressor = null; // { fn, timeoutId } or null
    let autoScrollRafId = null; // rAF id while an armed drag is auto-scrolling near an edge, else null
    let autoScrollDx = 0;       // signed px/frame the scroll container is currently being nudged by; 0 = inactive

    const gatherColumnCells = (th, colIndex, tbody) => {
        const cells = [th];
        if (tbody && colIndex >= 0) {
            for (const row of tbody.rows) {
                const cell = row.children[colIndex];
                if (cell && !(cell.colSpan && cell.colSpan > 1)) cells.push(cell);
            }
        }
        return cells;
    };

    // Mirrors DataGrid.ReorderColumnsPreservingLocked's two-pass algorithm:
    // pass 1 is the plain RemoveAt(srcIdx)+Insert(targetIdx) over the FULL
    // headers array (using the raw pre-removal targetIdx as the post-removal
    // insert index — same "insert before vs after" trick the .NET side
    // relies on); pass 2 splices locked (non-reorderable) headers back into
    // their ORIGINAL absolute slots and drains the reorderable headers (in
    // pass 1's relative order) into the remaining ones. Returns the
    // resulting header order — same shape/length as drag.headers.
    const projectColumnFinalOrder = (headers, srcIdx, targetIdx) => {
        const naive = headers.slice();
        const moving = naive[srcIdx];
        naive.splice(srcIdx, 1);
        naive.splice(Math.min(targetIdx, naive.length), 0, moving);
        const reorderableInOrder = naive.filter((h) => h.reorderable);
        const finalOrder = new Array(headers.length);
        let next = 0;
        for (let i = 0; i < headers.length; i++) {
            finalOrder[i] = headers[i].reorderable ? reorderableInOrder[next++] : headers[i];
        }
        return finalOrder;
    };

    // Computes each header's live-shift transform (including the dragged
    // header itself, used by finishDrag's settle) from the locked-preserving
    // final order above, instead of a uniform ±sourceWidth shift. Locked
    // headers are un-displaceable — the .NET-side commit guard
    // (ReorderColumnByIdAsync) refuses any drop whose target isn't
    // Reorderable, so they never receive a transform and anchor the walk at
    // their OWN original rect; every reorderable header (including the one
    // being dragged) packs left-to-right from there using its own cached
    // width. This degrades to the old formula exactly when no locked column
    // sits between source and target (verified: both reduce to the same
    // ±sourceWidth shift for an all-reorderable partition), and — unlike the
    // old formula — no longer overlaps a locked column the drag skips over
    // (Codex round-4 #6).
    const computeColumnLayoutShifts = (headers, srcIdx, targetIdx) => {
        const finalOrder = projectColumnFinalOrder(headers, srcIdx, targetIdx);
        const shifts = new Map();
        let cursor = headers[0].baseRect.left;
        for (const h of finalOrder) {
            if (h.reorderable) {
                shifts.set(h, cursor - h.baseRect.left);
                cursor += h.baseRect.width;
            } else {
                shifts.set(h, 0);
                cursor = h.baseRect.right;
            }
        }
        return shifts;
    };

    // Applies (or re-applies) the live sibling-shift preview for a projected
    // insertion index. Guarded by drag.lastProjectedIdx so it only runs when the
    // index actually changes (a handful of times per drag, never per move), and
    // only writes cells whose own target transform changed.
    const applyProjection = (targetIdx) => {
        if (drag.lastProjectedIdx === targetIdx) return;
        drag.lastProjectedIdx = targetIdx;
        const srcIdx = drag.srcHeaderIdx;
        const shifts = computeColumnLayoutShifts(drag.headers, srcIdx, targetIdx);
        for (let i = 0; i < drag.headers.length; i++) {
            if (i === srcIdx) continue;
            const h = drag.headers[i];
            // Locked (Reorderable=false) columns are un-displaceable — the
            // .NET-side commit guard (ReorderColumnByIdAsync) refuses any drop
            // whose target isn't Reorderable, so the live preview must never
            // shift one aside either. The dragged column glides past it in
            // place instead of pushing it. (shifts.get already yields 0 for
            // a locked header — this guard is defense-in-depth.)
            if (!h.reorderable) continue;
            const tx = shifts.get(h) || 0;
            if (h.appliedTx === tx) continue;
            h.appliedTx = tx;
            const t = tx ? `translateX(${tx}px)` : '';
            for (const cell of h.cells) {
                cell.style.transition = `transform ${REORDER_SHIFT_MS}ms ${REORDER_EASE}`;
                cell.style.transform = t;
            }
        }
    };

    // Same-pin header whose cached base rect contains clientX (clamped to the
    // partition ends). Returns an index into drag.headers — never reads layout,
    // baseRect was measured once at drag start. A locked header under the
    // pointer is never returned directly: it's un-displaceable (see
    // applyProjection above), so landing on one redirects to the nearest
    // reorderable header in the direction away from the source — the dragged
    // column "skips over" the lock instead of proposing it as a target the
    // .NET-side guard would reject.
    const computeTargetIdx = (clientX) => {
        let idx = -1;
        for (let i = 0; i < drag.headers.length; i++) {
            const r = drag.headers[i].baseRect;
            if (clientX >= r.left && clientX <= r.right) { idx = i; break; }
        }
        if (idx < 0) {
            const firstR = drag.headers[0].baseRect;
            idx = clientX < firstR.left ? 0 : drag.headers.length - 1;
        }
        if (drag.headers[idx].reorderable) return idx;
        const dir = idx >= drag.srcHeaderIdx ? 1 : -1;
        let j = idx;
        while (j >= 0 && j < drag.headers.length && !drag.headers[j].reorderable) j += dir;
        // Nothing reorderable in that direction (e.g. every remaining column in
        // the partition is locked) — stay put, no-op projection.
        return (j < 0 || j >= drag.headers.length) ? drag.srcHeaderIdx : j;
    };

    const reducedMotion = () => !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    const clearPendingLongPressTimer = (d) => {
        if (d && d.longPressTimer) { window.clearTimeout(d.longPressTimer); d.longPressTimer = null; }
    };

    const clearSortClickSuppressor = () => {
        if (!pendingSortClickSuppressor) return;
        document.removeEventListener('click', pendingSortClickSuppressor.fn, true);
        window.clearTimeout(pendingSortClickSuppressor.timeoutId);
        pendingSortClickSuppressor = null;
    };

    // Click-suppression (whole-surface sort click vs. title-drag, redesign point 2):
    // once a header-wide (non-grip) drag actually ARMS, the eventual pointerup would
    // otherwise still be followed by a real 'click' MouseEvent wherever the pointer
    // visually released — pointer CAPTURE (set right below, in armDrag) retargets
    // pointer events to the capturing element but does NOT retarget the synthesized
    // 'click', so without this the completed/cancelled drag would also fire
    // HandleSort on the sort button. A capture-phase listener on `document` runs
    // BEFORE Blazor's own delegated bubble-phase click dispatcher (capture always
    // finishes before bubble begins for the same event), so swallowing it here is
    // reliable regardless of where Blazor's own listener is rooted. Always self-
    // removes on the very next click (exactly once) — but only actually prevents it
    // when that click landed inside the dragged header, so an unrelated click
    // elsewhere in the app right after a drag is never eaten. The 500ms timeout is a
    // safety net for the case no click ever arrives at all (e.g. released outside
    // the document).
    const armSortClickSuppression = (th) => {
        clearSortClickSuppressor();
        const fn = (e) => {
            if (th.contains(e.target)) {
                e.preventDefault();
                e.stopPropagation();
            }
            clearSortClickSuppressor();
        };
        const timeoutId = window.setTimeout(clearSortClickSuppressor, 500);
        pendingSortClickSuppressor = { fn, timeoutId };
        document.addEventListener('click', fn, true);
    };

    const stopAutoScroll = () => {
        if (autoScrollRafId !== null) { window.cancelAnimationFrame(autoScrollRafId); autoScrollRafId = null; }
        autoScrollDx = 0;
    };

    // Non-reorderable "can't drag this" feedback (redesign point 7): a tiny 2px
    // nudge-and-spring on the header, driven entirely by a CSS keyframe animation
    // (lumeo.css) so it never touches `transform` inline (which only ever belongs to
    // the live-drag engine for actually-reorderable columns).
    const triggerNonReorderableNudge = (th) => {
        th.classList.remove('lumeo-dg-reorder-nudge');
        void th.offsetWidth; // restart the animation even if a previous nudge on this header hasn't cleared yet
        th.classList.add('lumeo-dg-reorder-nudge');
        window.setTimeout(() => th.classList.remove('lumeo-dg-reorder-nudge'), REORDER_NUDGE_MS + 20);
    };

    // Unified drag-to-group (rc.42): hit-tests the pointer against the group
    // panel's cached rect (measured once at pointerdown in onPointerDown — the
    // panel doesn't move during a column drag, only the table body's own
    // `.overflow-auto` wrapper scrolls) and toggles drag.mode between 'reorder'
    // and 'panel'. Entering panel mode is what lets applyLiveTranslate below
    // relax the live sibling-shift preview back to identity — the header row
    // visually "lets go" the instant the pointer crosses into the panel, mirroring
    // what leaving a valid native-DnD drop target used to look like. The
    // accept-highlight (GROUP_PANEL_DROP_ATTR) only ever lights up when the
    // dragged column is actually Groupable (drag.groupable, cached from its
    // data-groupable attribute at pointerdown) — a non-groupable column hovering
    // the panel must never look acceptable, since dropping it there is a no-op
    // cancel (see onPointerUp).
    const updateGroupPanelMode = (clientX, clientY) => {
        if (!drag) return;
        const r = drag.panelRect;
        const overPanel = !!r && clientX >= r.left && clientX <= r.right && clientY >= r.top && clientY <= r.bottom;
        const nextMode = overPanel ? 'panel' : 'reorder';
        if (drag.mode === nextMode) return;
        drag.mode = nextMode;
        if (!drag.panelEl) return;
        if (nextMode === 'panel' && drag.groupable) drag.panelEl.setAttribute(GROUP_PANEL_DROP_ATTR, 'true');
        else drag.panelEl.removeAttribute(GROUP_PANEL_DROP_ATTR);
    };

    // Best-effort column title for the ghost's label. Mirrors
    // DataGridHeaderCell.razor's title markup — the sort button's own `.truncate`
    // span holds exactly `Column.Title ?? Column.Field` and nothing else (icons/
    // badges are separate elements). Falls back to the header cell's own trimmed
    // text for a consumer HeaderTemplate (Column.HeaderTemplate is not null, which
    // skips the sort button entirely) so a custom header still gets SOME label
    // rather than an empty chip — stripped of the reorder grip (and any other
    // button-hosted zone-A/C control) on a clone first, so a decorative glyph
    // never leaks into the ghost's label.
    const resolveColumnTitle = (th) => {
        const titleEl = th.querySelector('[data-slot="datagrid-sort-button"] .truncate');
        if (titleEl) return titleEl.textContent.trim();
        const clone = th.cloneNode(true);
        clone.querySelectorAll('[data-reorder-grip], button').forEach((n) => n.remove());
        return (clone.textContent || '').trim();
    };

    // Builds the one ghost element for this drag (see GHOST_CHIP_CLASS's comment),
    // appended to document.body so it's clipped by nothing regardless of how deep
    // the grid is nested (a scrolling container, a Dialog/Sheet, etc.). Starts
    // invisible/off-screen — showGhost is what actually reveals+positions it, the
    // first time the pointer carries outside the header row's band.
    const createGhostEl = (title) => {
        const el = document.createElement('div');
        el.setAttribute('data-slot', 'datagrid-drag-ghost');
        // Purely visual — DataGrid.razor's aria-live _columnReorderAnnouncement
        // region already covers a11y for the reorder/group gesture itself.
        el.setAttribute('aria-hidden', 'true');
        el.className = GHOST_CHIP_CLASS;
        el.style.position = 'fixed';
        el.style.left = '0';
        el.style.top = '0';
        el.style.margin = '0';
        el.style.pointerEvents = 'none';
        el.style.zIndex = String(GHOST_Z_INDEX);
        el.style.opacity = '0';
        el.style.willChange = 'transform, opacity';
        el.innerHTML = GHOST_ICON_SVG;
        const label = document.createElement('span');
        label.className = 'truncate';
        label.textContent = title;
        el.appendChild(label);
        document.body.appendChild(el);
        return el;
    };

    // Reveals/repositions the ghost for the current pointer position — called on
    // every pointermove while carrying (see updateGhostCarry). transition is forced
    // to 'none' on every call so it tracks the pointer 1:1 with zero lag; finishDrag
    // (via teardownGhost) is the only place that ever sets a real transition on this
    // element, for the exit/cancel animations. Caches the last tracked point on
    // drag.ghostLastX/Y so teardownGhost's group-panel exit animation knows exactly
    // where to scale+fade from without re-reading layout.
    const showGhost = (clientX, clientY) => {
        const el = drag.ghostEl;
        drag.ghostLastX = clientX + drag.ghostOffsetX;
        drag.ghostLastY = clientY + drag.ghostOffsetY;
        el.style.transition = 'none';
        el.style.transform = `translate3d(${drag.ghostLastX}px, ${drag.ghostLastY}px, 0)`;
        if (!drag.ghostVisible) {
            drag.ghostVisible = true;
            el.style.opacity = '1';
            // Dim the origin a touch more while the ghost is the one carrying the
            // "this is what's moving" visual — reuses the SAME opacity channel
            // armDrag already dropped to 0.8 for the whole drag, just deeper, rather
            // than inventing a second dim mechanism.
            for (const cell of drag.cells) cell.style.opacity = '0.4';
        }
    };

    // Hides the ghost and restores the origin's normal (0.8) drag dim — the real
    // header cells carry the drag visual again from here.
    const hideGhost = () => {
        if (!drag.ghostVisible) return;
        drag.ghostVisible = false;
        drag.ghostEl.style.opacity = '0';
        for (const cell of drag.cells) cell.style.opacity = '0.8';
    };

    // Toggles the ghost between hidden (real header carries the visual — pointer is
    // inside the header row's band) and showing-and-following (the header "lets
    // go", the ghost carries instead — pointer is in panel mode, or simply outside
    // the band by more than GHOST_BAND_SLOP_PX). No-op when this drag never got a
    // ghost at all (non-Groupable column, or a grid with no group panel).
    //
    // A Groupable-only drag (drag.canReorder false) never hides it again once
    // armDrag showed it immediately — grouping is that drag's ONLY meaning, so
    // there's no "still deciding, header carries the visual" phase for the
    // band-exit rule to gate: the ghost stays the drag's sole visual for its
    // entire lifetime, in-band or not.
    const updateGhostCarry = (clientX, clientY) => {
        if (!drag.ghostEl) return;
        if (!drag.canReorder) { showGhost(clientX, clientY); return; }
        const r = drag.headerRowRect;
        const inBand = clientY >= r.top - GHOST_BAND_SLOP_PX && clientY <= r.bottom + GHOST_BAND_SLOP_PX;
        if (drag.mode === 'panel' || !inBand) showGhost(clientX, clientY);
        else hideGhost();
    };

    // Ends the ghost for this drag — finishDrag calls this exactly once, covering
    // every end path (commit, cancel, Escape, pointercancel) since they all funnel
    // through it. A drop that actually commits a NEW grouping level gets the
    // "lands as the chip" exit: scale+fade at the last tracked point, so the
    // hand-off to the real rendered chip (DataGrid.razor's data-slot=
    // "datagrid-group-panel") reads as one continuous motion. Every other end path
    // just fades the ghost away in place, no scale. Both skip straight to removal
    // under prefers-reduced-motion, matching every other reduced-motion branch in
    // this engine (armDrag's own lift, above). Always nulls d.ghostEl FIRST so a
    // stray double-call can never double-remove/double-animate, and so a drag that
    // never got a ghost (the common case) is a cheap no-op.
    const teardownGhost = (d, groupCommit) => {
        const el = d.ghostEl;
        if (!el) return;
        d.ghostEl = null;
        if (!d.ghostVisible || reducedMotion()) { el.remove(); return; }
        const ms = groupCommit ? GHOST_EXIT_MS : GHOST_CANCEL_FADE_MS;
        el.style.transition = `transform ${ms}ms ${REORDER_EASE}, opacity ${ms}ms ${REORDER_EASE}`;
        el.style.opacity = '0';
        el.style.transform = groupCommit
            ? `translate3d(${d.ghostLastX}px, ${d.ghostLastY}px, 0) scale(0.55)`
            : `translate3d(${d.ghostLastX}px, ${d.ghostLastY}px, 0)`;
        window.setTimeout(() => el.remove(), ms + 20);
    };

    // Writes the live drag-follow transform for the current pointer position,
    // composing the header's lift scale into the SAME transform string (never a
    // separate CSS rule — an inline `style.transform` write always wins outright
    // over a class's `transform`, so a class-authored scale would simply be
    // discarded the instant this runs). Factored out of onPointerMove so the
    // auto-scroll rAF loop (which must keep the dragged column glued to a
    // STATIONARY pointer while the container scrolls under it) can re-invoke the
    // exact same math on every scrolled frame, not just on real pointermoves.
    // scrollDelta compensates bounds/tx for however far the container has scrolled
    // since drag start — when auto-scroll never triggers, scrollDelta is always 0
    // and every formula below reduces exactly to the pre-redesign math.
    const applyLiveTranslate = (clientX) => {
        if (!drag) return;
        const sc = drag.scrollContainer;
        const scrollDelta = sc ? (sc.scrollLeft - drag.startScrollLeft) : 0;
        let tx = (clientX - drag.startX) + scrollDelta;
        const minTx = (drag.bounds.min - drag.sourceRect.left) + scrollDelta;
        const maxTx = (drag.bounds.max - drag.sourceRect.right) + scrollDelta;
        if (tx < minTx) tx = minTx;
        if (tx > maxTx) tx = maxTx;
        const translate = tx ? `translateX(${tx}px)` : '';
        for (let i = 0; i < drag.cells.length; i++) {
            const lift = i === 0 ? drag.liftTransform : '';
            drag.cells[i].style.transform = lift ? (translate ? `${translate} ${lift}` : lift) : translate;
        }
        // 'panel' mode relaxes the sibling preview back to identity (project onto
        // the drag's own original index) instead of tracking computeTargetIdx —
        // the dragged header keeps following the pointer horizontally, but nothing
        // else moves while a group-drop is pending.
        applyProjection(drag.mode === 'panel' ? drag.srcHeaderIdx : computeTargetIdx(clientX + scrollDelta));
    };

    const autoScrollTick = () => {
        if (!drag || !drag.armed || autoScrollDx === 0) { autoScrollRafId = null; return; }
        const sc = drag.scrollContainer;
        if (sc && sc.scrollWidth > sc.clientWidth) {
            const max = sc.scrollWidth - sc.clientWidth;
            const next = Math.max(0, Math.min(max, sc.scrollLeft + autoScrollDx));
            if (next !== sc.scrollLeft) {
                sc.scrollLeft = next;
                applyLiveTranslate(drag.lastClientX);
            }
        }
        autoScrollRafId = window.requestAnimationFrame(autoScrollTick);
    };

    // Gentle accelerating auto-scroll near the scroll container's edges while an
    // armed drag is live (redesign point 8 — this engine previously had none at
    // all). Speed ramps linearly from 0 at REORDER_AUTOSCROLL_EDGE_PX away from the
    // edge up to REORDER_AUTOSCROLL_MAX_PX/frame right at the edge.
    const updateAutoScroll = (clientX) => {
        const sc = drag && drag.scrollContainer;
        if (!sc || sc.scrollWidth <= sc.clientWidth) { stopAutoScroll(); return; }
        const rect = sc.getBoundingClientRect();
        let dx = 0;
        if (clientX < rect.left + REORDER_AUTOSCROLL_EDGE_PX) {
            const depth = Math.min(1, (rect.left + REORDER_AUTOSCROLL_EDGE_PX - clientX) / REORDER_AUTOSCROLL_EDGE_PX);
            dx = -Math.ceil(depth * REORDER_AUTOSCROLL_MAX_PX);
        } else if (clientX > rect.right - REORDER_AUTOSCROLL_EDGE_PX) {
            const depth = Math.min(1, (clientX - (rect.right - REORDER_AUTOSCROLL_EDGE_PX)) / REORDER_AUTOSCROLL_EDGE_PX);
            dx = Math.ceil(depth * REORDER_AUTOSCROLL_MAX_PX);
        }
        autoScrollDx = dx;
        if (dx !== 0 && autoScrollRafId === null) autoScrollRafId = window.requestAnimationFrame(autoScrollTick);
        else if (dx === 0) stopAutoScroll();
    };

    // clientX/clientY: the pointer position at the moment this drag actually
    // arms — passed in (rather than read off `drag.lastClientX`, which isn't
    // set until the FIRST armed pointermove) so a Groupable-only drag
    // (drag.canReorder false) can show its ghost immediately below, before any
    // move has happened at all.
    const armDrag = (clientX, clientY) => {
        drag.armed = true;
        // Saved BEFORE overwriting so finishDrag can restore whatever a
        // consumer's own ColumnStyle inline declaration had on these four
        // properties, instead of blanking them to '' (Codex round-4 #4).
        drag.savedCellStyles = drag.cells.map((cell) => captureDragStyles(cell));
        // A pinned column's cells hold `position: sticky` from the CSS-authored
        // pinned classes; forcing an inline `position: relative` here overrides
        // it and snaps the cell back into normal table flow for the duration of
        // the drag, letting it visually jump away from the pinned edge before
        // any transform lands (Codex round-5 #5). Skip the position write for
        // pinned cells — `position: sticky` still counts as positioned for
        // z-index purposes, so the opacity/z-index lift keeps working.
        const keepSticky = drag.sourcePin !== 'None';
        for (const cell of drag.cells) {
            cell.style.transition = 'none';
            cell.style.opacity = '0.8';
            if (!keepSticky) cell.style.position = 'relative';
            cell.style.zIndex = '2';
            cell.style.pointerEvents = 'none';
        }
        document.body.style.cursor = 'grabbing';
        document.documentElement.style.cursor = 'grabbing';
        document.body.style.userSelect = 'none';
        try { drag.captureTarget.setPointerCapture(drag.pointerId); } catch (_) { }
        window.addEventListener('keydown', onKeyDown, true);

        // Lift affordance (subtle, redesign point 4): the dragged HEADER cell
        // (cells[0] — gatherColumnCells always puts th first) gets a brief
        // scale(1.03) + drop-shadow pop, composed into the transform applyLiveTranslate
        // writes rather than a separate CSS rule (see that function's comment). Neither
        // the live-drag loop's own transform nor finishDrag's settle transform ever
        // re-applies liftTransform once cleared, so the scale eases back to 1 for free
        // as part of the normal settle glide — no separate "un-lift" animation needed.
        // The one-shot transition below is stripped again once the pop-in completes so
        // continuous translateX tracking stays lag-free for the rest of the drag
        // (mirrors the `transition = 'none'` reasoning on drag.cells just above).
        // Respects prefers-reduced-motion by skipping the scale (shadow still applies).
        const headerCell = drag.cells[0];
        drag.liftTransform = reducedMotion() ? '' : 'scale(1.03)';
        headerCell.style.boxShadow = 'var(--lumeo-dg-reorder-lift-shadow, 0 10px 24px -6px rgb(0 0 0 / 0.35))';
        headerCell.style.transition = `transform ${REORDER_LIFT_MS}ms ease-out, box-shadow ${REORDER_LIFT_MS}ms ease-out`;
        if (drag.liftTransform) headerCell.style.transform = drag.liftTransform;
        const armedDrag = drag;
        window.setTimeout(() => {
            if (drag !== armedDrag) return; // released/cancelled/re-armed before the pop-in finished — settle owns transition now
            headerCell.style.transition = 'none';
        }, REORDER_LIFT_MS + 10);

        // Click-suppression (redesign point 2) only applies to a header-wide
        // (non-grip) arm — a grip-initiated drag has pointer capture on the grip
        // itself (a sibling of the sort button), so its eventual click can never
        // land on the button in the first place.
        if (drag.suppressSortClick) armSortClickSuppression(headerCell);

        // Floating chip ghost (see GHOST_CHIP_CLASS's comment above) — only
        // relevant when this drag could actually end in a group-panel drop: a grid
        // with no group panel, or a non-Groupable column, never gets one (mirrors
        // the panel accept-highlight's own groupable gate in updateGroupPanelMode).
        if (drag.panelEl && drag.groupable) {
            drag.ghostEl = createGhostEl(resolveColumnTitle(headerCell));
            drag.ghostVisible = false;
            drag.ghostOffsetX = GHOST_MOUSE_OFFSET;
            drag.ghostOffsetY = drag.pointerType === 'touch' ? GHOST_TOUCH_OFFSET_Y : GHOST_MOUSE_OFFSET;
            drag.ghostLastX = 0;
            drag.ghostLastY = 0;
            // Groupable-only drag (not Reorderable): grouping is the drag's ONLY
            // possible meaning, so the ghost previews it from the very first
            // instant — no reorder preview ever runs for this drag (see
            // applyLiveTranslate's canReorder gate) and updateGhostCarry's own
            // header-row-exit band rule is what would otherwise delay this until
            // the pointer physically left the header row. showGhost also dims the
            // real header cell to 0.4 opacity, same as any other carrying ghost.
            if (!drag.canReorder) showGhost(clientX, clientY);
        } else {
            drag.ghostEl = null;
        }
    };

    const onKeyDown = (e) => {
        if (e.key === 'Escape' && drag) {
            e.preventDefault();
            finishDrag(null);
        }
    };

    // A header-wide mouse init leaves `drag` pending-unarmed with no pointer
    // capture (see onPointerDown below), so a release OUTSIDE the grid never
    // reaches this grid's own onPointerUp. The pointermove `buttons` check
    // (Codex round-3 #4) only drops that stale descriptor on the NEXT move
    // over this grid — mouse pointerId reuse means an unrelated later
    // press-and-drag could still re-arm it before any move ever crosses the
    // grid. A window-level pointerup clears any pending descriptor the
    // moment its button is released anywhere, closing that window (Codex
    // round-4 #5).
    const onWindowPointerUp = () => {
        // The arbiter token was claimed at the SAME pointerdown that set
        // `drag` (see onPointerDown below) — an unarmed descriptor dropped
        // here never reaches finishDrag, so the release has to happen here
        // too, or a plain click that starts (but never crosses the movement
        // threshold for) a header-wide mouse init would strand the token
        // claimed and permanently block every other engine on this grid.
        // Mirrors the same reasoning for a pending touch/pen long-press timer
        // and the non-reorderable nudge watch — neither survives a release
        // anywhere in the window.
        nudgeWatch = null;
        if (drag && !drag.armed) { clearPendingLongPressTimer(drag); drag = null; releaseGridDrag(gridId, 'column-reorder'); }
    };

    // Ends the drag. commitTargetColId is null for cancel (Escape / pointercancel)
    // — everything animates back to identity, nothing is committed. For a valid
    // drop, the dragged column glides to its projected slot (siblings are already
    // resting at their live-shift positions — nothing more to animate for them);
    // once that settle finishes, OnColumnReorderCommit fires exactly once.
    //
    // The arbiter token claimed at pointerdown is held through the WHOLE settle
    // window, not released here — round-10 finding #3: releasing it immediately
    // (the old behavior) let a new column/row reorder or resize claim the same
    // grid while this pendingSettle timeout was still going to invoke .NET,
    // so a stale queued commit could mutate/re-render the grid while another
    // gesture owned live transforms. The token is only released once the
    // settle timeout actually runs (below) or cancelActiveDrag tears down a
    // still-pending settle (unregisterColumnReorder racing this window).
    const finishDrag = (commitTargetColId) => {
        const d = drag;
        drag = null;
        clearPendingLongPressTimer(d);
        stopAutoScroll();
        window.removeEventListener('keydown', onKeyDown, true);
        document.body.style.cursor = '';
        document.documentElement.style.cursor = '';
        document.body.style.userSelect = '';
        // Unconditional, unconditionally-safe cleanup for the group-panel highlight
        // (rc.42) — every drag end path (commit, cancel, Escape, pointercancel)
        // funnels through here, so this is the ONE place that needs to guarantee
        // the panel never stays highlighted after the pointer that triggered it is
        // gone. No-op when the attribute was never set.
        if (d.panelEl) d.panelEl.removeAttribute(GROUP_PANEL_DROP_ATTR);
        if (!d.armed) { releaseGridDrag(gridId, 'column-reorder'); return; } // never engaged (plain click, or a touch/pen long-press that never fired) — nothing to animate/commit
        try { d.captureTarget.releasePointerCapture(d.pointerId); } catch (_) { }

        const isCommit = !!commitTargetColId;
        // Floating chip ghost teardown — the SAME single funnel every drag end path
        // (commit, cancel, Escape, pointercancel) already runs through, so this is
        // the ONE place that needs to guarantee the ghost never outlives its drag. A
        // no-op when this drag never got a ghost at all. Only a drop that actually
        // committed a NEW group level gets the "lands as the chip" exit — see
        // teardownGhost's own comment.
        teardownGhost(d, commitTargetColId === GROUP_PANEL_DROP_TARGET_ID);
        const shiftedCells = d.headers.filter((h) => h.appliedTx).flatMap((h) => h.cells);
        // Settle position for the dragged column itself comes from the exact
        // same locked-preserving projection applyProjection used for its
        // siblings (computeColumnLayoutShifts) — the dragged header's own
        // entry in that map IS its final-order position, so this stays
        // correct even when a locked column sits between source and target
        // (Codex round-4 #6; previously a target-left/target-right shortcut
        // that ignored locked columns entirely).
        const draggedTx = isCommit
            ? (computeColumnLayoutShifts(d.headers, d.srcHeaderIdx, d.lastProjectedIdx).get(d.headers[d.srcHeaderIdx]) || 0)
            : 0;

        d.cells.forEach((cell, i) => {
            cell.style.transition = `transform ${REORDER_SETTLE_MS}ms ${REORDER_EASE}`;
            cell.style.transform = draggedTx ? `translateX(${draggedTx}px)` : '';
            restoreDragStyles(cell, d.savedCellStyles[i]);
        });
        if (!isCommit) {
            // Cancel: nothing is going to re-render the grid, so siblings must
            // glide back to identity here too — there's no FLIP pass to hand off to.
            for (const cell of shiftedCells) {
                cell.style.transition = `transform ${REORDER_SETTLE_MS}ms ${REORDER_EASE}`;
                cell.style.transform = '';
            }
        }

        const settleCells = new Set([...d.cells, ...shiftedCells]);
        // Recorded by element reference (round-11 #1) — see columnReorderSettleEls.
        columnReorderSettleEls.set(gridId, settleCells);
        const timeoutId = window.setTimeout(() => {
            for (const cell of d.cells) cell.style.transition = '';
            if (isCommit) {
                // Leave the settled transforms in place (dragged column at its
                // projected slot, siblings at their live-shift positions) —
                // captureColumnRects measures this true final visual order next
                // and clears these inline styles itself.
                //
                // pendingSettle stays set (NOT nulled here — round-13 #4) through the
                // commit interop call itself, not just this timeout. The old code
                // nulled it synchronously the instant this timeout fired, before
                // invokeMethodAsync even started — if unregisterColumnReorder raced
                // that in-flight window (Reorderable flipped off mid-commit, plain
                // Blazor Server latency), cancelActiveDrag saw pendingSettle === null
                // and skipped its cleanup branch entirely, even though
                // columnReorderSettleEls still held these exact cells: nothing would
                // ever strip their transforms, since no future FLIP pass was coming
                // (captureColumnRects/clearColumnReorderTransforms — which own
                // columnReorderSettleEls's normal lifecycle, deleting it themselves —
                // are only reached from a commit that actually lands, which a
                // torn-down component will never receive). pendingSettle is only
                // cleared in this promise's `finally` (columnReorderSettleEls is
                // deliberately left untouched here — it stays exactly whichever of
                // captureColumnRects/clearColumnReorderTransforms's job it already
                // was), so a cancelActiveDrag landing mid-flight still finds
                // pendingSettle and strips the transforms itself. The token is
                // released from the SAME `finally`, not synchronously right after
                // invokeMethodAsync starts (round-12 #2) — see that finding for why.
                // `finally` covers a rejected commit too.
                dotnetRef.invokeMethodAsync('OnColumnReorderCommit', gridId, d.sourceColId, commitTargetColId)
                    .finally(() => {
                        pendingSettle = null;
                        releaseGridDrag(gridId, 'column-reorder');
                    });
            } else {
                for (const cell of shiftedCells) {
                    cell.style.transition = '';
                    cell.style.transform = '';
                }
                // No commit fires on cancel — nothing to await, release now.
                pendingSettle = null;
                releaseGridDrag(gridId, 'column-reorder');
            }
        }, REORDER_SETTLE_MS + 20);
        pendingSettle = { timeoutId, cells: settleCells };
    };

    const onPointerMove = (e) => {
        // Non-reorderable "attempted drag" nudge watch runs independently of `drag`
        // (it never claims the arbiter) — check it first so it still fires even
        // while a DIFFERENT column's real drag isn't live.
        if (nudgeWatch && e.pointerId === nudgeWatch.pointerId && !nudgeWatch.firedNudge) {
            const ndx = e.clientX - nudgeWatch.startX, ndy = e.clientY - nudgeWatch.startY;
            if ((ndx * ndx + ndy * ndy) >= REORDER_MOVE_THRESHOLD * REORDER_MOVE_THRESHOLD) {
                nudgeWatch.firedNudge = true;
                triggerNonReorderableNudge(nudgeWatch.th);
            }
        }
        if (!drag || e.pointerId !== drag.pointerId) return;
        if (!drag.armed) {
            if (drag.pointerType === 'mouse') {
                // Header-wide mouse init stays unarmed (no pointer capture) until
                // the movement threshold is crossed, so a release OUTSIDE the grid
                // never reaches this grid's own pointerup listener and the pending
                // drag would otherwise linger forever. Mouse pointerIds are reused,
                // so a LATER move back over the grid — with no button held — could
                // still cross the threshold and arm a phantom drag. e.buttons
                // always reflects the live button state regardless of capture, so
                // drop the stale descriptor the moment the primary button isn't
                // held anymore instead of arming (Codex round-3 #4).
                if (!(e.buttons & 1)) {
                    // Mirrors onWindowPointerUp's release below: the arbiter claim
                    // was taken in onPointerDown BEFORE `drag` even existed (right
                    // before the descriptor is constructed, regardless of arm
                    // state — see that claim's comment), so every path that drops
                    // an unarmed descriptor — this stale-move fallback included —
                    // must release it too, or a release outside the window/browser
                    // (which never reaches onWindowPointerUp either, e.g. the tab
                    // loses focus before any pointerup fires) leaks the token and
                    // permanently blocks every later drag on this grid (round-7 #2).
                    drag = null;
                    releaseGridDrag(gridId, 'column-reorder');
                    return;
                }
                const dx = e.clientX - drag.startX, dy = e.clientY - drag.startY;
                if ((dx * dx + dy * dy) < REORDER_MOVE_THRESHOLD * REORDER_MOVE_THRESHOLD) return;
                armDrag(e.clientX, e.clientY);
            } else {
                // Touch/pen header-wide long-press arming (zone B, redesign point 3):
                // movement alone never arms a header-wide touch/pen drag — only the
                // long-press timer scheduled in onPointerDown does. Crossing
                // REORDER_LONGPRESS_CANCEL_PX BEFORE that timer fires cancels the
                // pending arm entirely (never preventDefault'd, never captured) so
                // native scroll/pan wins, same as a real press-and-hold gesture.
                const dx = e.clientX - drag.startX, dy = e.clientY - drag.startY;
                if ((dx * dx + dy * dy) >= REORDER_LONGPRESS_CANCEL_PX * REORDER_LONGPRESS_CANCEL_PX) {
                    clearPendingLongPressTimer(drag);
                    drag = null;
                    releaseGridDrag(gridId, 'column-reorder');
                }
                return;
            }
        }
        if (e.cancelable) e.preventDefault();
        drag.lastClientX = e.clientX;
        updateGroupPanelMode(e.clientX, e.clientY);
        // Groupable-only drag (drag.canReorder false): grouping is this drag's
        // ONLY meaning — there is no reorder preview to run at all (no sibling
        // shift, no in-row follow-the-pointer transform beyond the lift styling
        // armDrag already applied to the origin cell), so applyLiveTranslate
        // (which drives BOTH) is skipped entirely rather than special-cased
        // inside it.
        if (drag.canReorder) applyLiveTranslate(e.clientX);
        // Floating chip ghost: re-evaluated AFTER updateGroupPanelMode so it sees
        // this move's up-to-date drag.mode (panel vs reorder).
        updateGhostCarry(e.clientX, e.clientY);
        // No point auto-scrolling the row area while the pointer is hovering the
        // (non-scrolling) group panel above it — and it would fight applyLiveTranslate's
        // reset-to-identity projection in 'panel' mode. A Groupable-only drag never
        // auto-scrolls either — there's no reorder preview or sibling-shift math
        // that scrolling-while-dragging was ever in service of for it.
        if (drag.mode === 'panel' || !drag.canReorder) stopAutoScroll();
        else updateAutoScroll(e.clientX);
    };

    const onPointerUp = (e) => {
        if (nudgeWatch && e.pointerId === nudgeWatch.pointerId) nudgeWatch = null;
        if (!drag || e.pointerId !== drag.pointerId) return;
        let commitTargetColId;
        if (drag.mode === 'panel') {
            // Groupable → commit to the group panel via the sentinel id (see
            // GROUP_PANEL_DROP_TARGET_ID / DataGrid.razor's GroupPanelDropTargetId).
            // Non-groupable → exactly like releasing outside any valid target: null
            // commits nothing, finishDrag glides everything back to identity.
            commitTargetColId = drag.groupable ? GROUP_PANEL_DROP_TARGET_ID : null;
        } else if (drag.canReorder) {
            const targetHeader = drag.headers[drag.lastProjectedIdx];
            commitTargetColId = targetHeader.colId !== drag.sourceColId ? targetHeader.colId : null;
        } else {
            // Groupable-only drag released anywhere other than the panel: a clean
            // cancel, exactly like releasing a reorderable drag back over its own
            // slot — there is no "target header" for a drag with no reorder
            // meaning at all.
            commitTargetColId = null;
        }
        finishDrag(commitTargetColId);
    };

    const onPointerCancel = (e) => {
        if (nudgeWatch && e.pointerId === nudgeWatch.pointerId) nudgeWatch = null;
        if (!drag || e.pointerId !== drag.pointerId) return;
        finishDrag(null);
    };

    const onPointerDown = (e) => {
        // Ignore non-primary mouse buttons; touch/pen always fall through —
        // mirrors registerColumnResize's and registerRowReorder's guard. The
        // grip branch below arms IMMEDIATELY (no movement-threshold to filter
        // it later), so without this a right- or middle-button press on the
        // grip would still claim the arbiter token, arm the drag, and
        // preventDefault — suppressing the column's context menu (round-12 #1).
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        // A second pointerdown while a drag is already pending/armed (second
        // touch point, second mouse button, concurrent pen) must not overwrite
        // the single `drag` descriptor — the live one owns its pointerId until
        // its own pointerup/pointercancel finishes it; a competing descriptor
        // would orphan the first drag's inline drag/transform styles forever
        // (Codex round-5 #3).
        if (drag) return;
        const grip = e.target.closest('[data-reorder-grip]');
        let th, viaGrip, viaLongPress = false;
        // A DataGrid nested inside another column-reorderable grid's
        // DetailTemplate/cell template sits inside this grid's DOM subtree
        // too, so grid.contains(grip) alone can't tell the two apart —
        // require the grip's OWN closest grid root to be this grid, not
        // just any ancestor of it, or the outer grid would also arm on the
        // inner grid's grips and commit against the inner header (mirrors
        // the row-reorder grip check; Codex round-4 #1).
        if (grip && grip.closest('[data-grid-id]') === grid) {
            th = grip.closest('th[data-col-id]');
            viaGrip = true;
        } else {
            // Header-wide initiation (zone B — redesign points 2/3): mouse arms via
            // a movement threshold, touch/pen via a long-press timer. Either way we
            // first need ANY header th under the pointer, reorderable or not, so a
            // non-reorderable column can still get its "can't drag this" nudge
            // feedback below instead of just doing nothing.
            if (e.pointerType !== 'mouse' && e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
            const anyTh = e.target.closest('th[data-col-id]');
            // Same nested-grid ownership requirement as the grip branch above — a
            // header-wide press inside an inner grid's header must not arm (or
            // nudge-watch) the OUTER grid either.
            if (!anyTh || anyTh.closest('[data-grid-id]') !== grid) return;
            // Defensive escape hatch, not a live path for OUR OWN grids any more:
            // rc.42 stopped ever setting draggable="true" on a header (drag-to-group
            // is unified into this same pointer engine — see updateGroupPanelMode /
            // GROUP_PANEL_DROP_TARGET_ID below). Kept so a consumer who explicitly
            // sets draggable="true" via AdditionalAttributes still opts that header
            // out of this engine, rather than the two silently fighting over the
            // same pointerdown.
            if (anyTh.getAttribute('draggable') === 'true') return;
            // Don't hijack interactive controls living inside the header — EXCEPT
            // the sort button itself, which IS zone B (redesign point 2): clicking
            // it sorts, dragging FROM it reorders. Every other control (filter/pin/
            // group-drag triggers, the resize handle) stays exclusive zone
            // A/C territory and never arms a reorder.
            const interactive = e.target.closest('button, a, input, select, textarea, [data-slot="datagrid-resize-handle"]');
            if (interactive && interactive.getAttribute('data-slot') !== 'datagrid-sort-button') return;

            // Grouping-by-drag must be independent of reorderability: a column
            // that isn't Reorderable but IS Groupable (with the panel actually
            // shown) still has to arm — the drag's only possible meaning there is
            // "drop on the group panel", never a row reorder (see canReorder/
            // canGroup below and every mode branch downstream of it). Only a
            // column that is NEITHER arms nothing at all and falls back to the
            // nudge.
            const canReorderHere = anyTh.dataset.reorderable === 'true';
            const canGroupHere = anyTh.dataset.groupable === 'true'
                && !!grid.querySelector('[data-slot="datagrid-group-panel"]');
            if (!canReorderHere && !canGroupHere) {
                // Neither reorderable nor groupable: never arms a drag, never
                // claims the grid arbiter — just watch for an attempted drag past
                // the same threshold a real one would use, to show the
                // nudge-and-spring "can't drag this" feedback (redesign point 7).
                // Sorting/clicking is completely untouched since nothing here
                // preventDefaults.
                nudgeWatch = { th: anyTh, pointerId: e.pointerId, startX: e.clientX, startY: e.clientY, firedNudge: false };
                return;
            }
            th = anyTh;
            viaGrip = false;
            viaLongPress = e.pointerType !== 'mouse';
        }
        if (!th) return;
        const sourceColId = th.dataset.colId;
        const sourcePin = th.dataset.colPin || 'None';
        // Cached once for the whole drag — the grip branch above only ever
        // targets a Reorderable column (the grip itself only renders for one —
        // see DataGridHeaderCell.razor), so this is always true there; the
        // header-wide branch already resolved the same flag as canReorderHere
        // before letting `th` through, re-derived here from the same dataset
        // attribute rather than threaded through as a parameter.
        const canReorderSrc = th.dataset.reorderable === 'true';

        const headerRow = th.parentElement;
        const table = th.closest('table');
        const tbody = table ? table.querySelector('tbody') : null;
        if (!headerRow || !table) return;

        // Same-pin partition candidates, with cached base rects AND cached cell
        // lists (header + body cells) — measured ONCE here so the live
        // sibling-shift preview never reads layout in the pointermove loop.
        const allTh = ownedByGrid(headerRow.querySelectorAll('th[data-col-id]'), grid);
        const partitionTh = allTh.filter((h) => (h.dataset.colPin || 'None') === sourcePin);
        // "Nothing to reorder within the partition" only matters when this drag
        // could actually reorder — a Groupable-only drag (canReorderSrc false)
        // has no sibling-shift preview at all (see applyLiveTranslate's
        // canReorder gate below) and is perfectly valid even as the sole column
        // in its pin partition, since its only possible destination is the
        // group panel, not a sibling header.
        if (canReorderSrc && partitionTh.length < 2) return;

        const headers = partitionTh.map((h) => {
            const idx = Array.prototype.indexOf.call(headerRow.children, h);
            return {
                th: h, colId: h.dataset.colId, baseRect: h.getBoundingClientRect(),
                cells: gatherColumnCells(h, idx, tbody), appliedTx: 0,
                // Reorderable=false columns stay in the partition array (they
                // still occupy screen space the projection math must account
                // for) but are excluded as displacement/drop targets — see
                // applyProjection / computeTargetIdx.
                reorderable: h.dataset.reorderable === 'true',
            };
        });
        // Sort into VISUAL left-to-right order. querySelectorAll returns DOM
        // (logical) order, which only matches visual order in LTR. In RTL, DOM
        // order is the visual mirror — leaving it unsorted inverts bounds.min/max
        // below (min > max), freezes the live drag translate at a fixed offset,
        // and clamps computeTargetIdx's past-the-ends fallback to the wrong end.
        // Index-diff math elsewhere (applyProjection, computeTargetIdx) assumes
        // ascending index === ascending screen x, which this sort guarantees.
        headers.sort((a, b) => a.baseRect.left - b.baseRect.left);
        const srcHeaderIdx = headers.findIndex((h) => h.colId === sourceColId);
        if (srcHeaderIdx < 0) return;

        const colIndex = Array.prototype.indexOf.call(headerRow.children, th);
        const cells = gatherColumnCells(th, colIndex, tbody);
        const sourceRect = th.getBoundingClientRect();
        const bounds = {
            min: headers[0].baseRect.left,
            max: headers[headers.length - 1].baseRect.right,
        };

        // Cross-engine: a resize (or row-reorder) already live on this grid
        // owns the shared arbiter token — refuse to start a competing column
        // reorder (round-6 finding; see the arbiter comment above
        // registerColumnResize). Claimed here, right before `drag` becomes
        // non-null, so a rejected claim never touches drag state.
        if (!claimGridDrag(gridId, 'column-reorder')) return;

        // The horizontal scroll container is the table's own parent (see the
        // `.overflow-auto` wrapper in DataGrid.razor) — derived from `table` rather
        // than a class-name lookup so it stays correct even if that wrapper's
        // class ever changes. Used only for auto-scroll-near-edge (redesign point
        // 8); startScrollLeft anchors applyLiveTranslate's scroll-compensation math
        // so the non-autoscrolling path (scrollDelta always 0) is untouched.
        const scrollContainer = table.parentElement;

        // Unified drag-to-group (rc.42): the group panel lives as a sibling of the
        // table inside this same grid root, present in the DOM only when
        // ShowGroupPanel is on — panelEl is simply null (and mode never becomes
        // 'panel') for a grid without one, so this measurement is a harmless no-op
        // there. Measured once here, like every other rect this drag cares about
        // (sourceRect, headers[].baseRect) — the panel doesn't move DURING a column
        // drag (only the table body's own scroll container does), so there's
        // nothing to re-measure on later moves.
        const panelEl = grid.querySelector('[data-slot="datagrid-group-panel"]');
        const panelRect = panelEl ? panelEl.getBoundingClientRect() : null;
        const groupable = th.dataset.groupable === 'true';
        // Floating chip ghost (see GHOST_CHIP_CLASS's comment): the header row's own
        // rect is the "band" the ghost's visibility rule hit-tests the pointer
        // against — measured once here, like every other rect this drag cares
        // about, since the row itself doesn't move during a column drag (only the
        // table body's own scroll container does).
        const headerRowRect = headerRow.getBoundingClientRect();

        drag = {
            pointerId: e.pointerId, captureTarget: viaGrip ? grip : th,
            startX: e.clientX, startY: e.clientY,
            sourceColId, sourcePin, cells, headers, srcHeaderIdx, sourceRect, bounds,
            lastProjectedIdx: srcHeaderIdx, armed: false,
            // The four-quadrant split (canReorder × canGroup) every mode branch
            // downstream keys off — see applyLiveTranslate's gate, armDrag's
            // immediate-ghost rule, updateGhostCarry's always-show rule, and
            // onPointerUp's release-anywhere-else cancel.
            canReorder: canReorderSrc,
            pointerType: e.pointerType, longPressTimer: null,
            // Only a header-wide (non-grip) arm needs the post-arm click swallow —
            // see armSortClickSuppression's comment.
            suppressSortClick: !viaGrip,
            liftTransform: '', lastClientX: e.clientX,
            scrollContainer, startScrollLeft: scrollContainer ? scrollContainer.scrollLeft : 0,
            // Drag-to-group (rc.42) — see updateGroupPanelMode.
            panelEl, panelRect, groupable, mode: 'reorder',
            // Floating chip ghost — see createGhostEl/updateGhostCarry/teardownGhost.
            // ghostEl is populated (or explicitly left null) by armDrag, once the
            // gesture actually arms; a click/nudge/cancelled-long-press that never
            // arms never gets one.
            headerRowRect, ghostEl: null, ghostVisible: false,
            ghostOffsetX: 0, ghostOffsetY: 0, ghostLastX: 0, ghostLastY: 0,
        };

        if (viaGrip) {
            armDrag(e.clientX, e.clientY);
            e.preventDefault();
            e.stopPropagation();
        } else if (viaLongPress) {
            // Touch/pen header-wide init (redesign point 3): schedule the long-press
            // arm timer. Captured by reference (not just nulled-check) so a stray
            // late-firing timer from an ALREADY-finished/cancelled drag can never
            // arm a new, unrelated one that happens to be pending at the same
            // moment (mirrors this file's pendingSettle-style identity guards).
            const pendingDrag = drag;
            const armX = e.clientX, armY = e.clientY; // pointer barely moves before a long-press fires — safe to reuse
            pendingDrag.longPressTimer = window.setTimeout(() => {
                if (drag !== pendingDrag) return;
                pendingDrag.longPressTimer = null;
                armDrag(armX, armY);
            }, REORDER_LONGPRESS_MS);
        }
        // Header-wide mouse init stays UNARMED here — no preventDefault/capture
        // yet, so a plain click still reaches its target if the pointer never
        // clears the movement threshold. armDrag() runs lazily from the first
        // pointermove that does (see onPointerMove). Header-wide touch/pen init
        // also stays unarmed and un-prevented here for the same reason — a short
        // tap must still reach the sort button's native click.
    };

    // Exposed so unregisterColumnReorder can fully abort an in-flight drag on
    // unmount — otherwise translated cells, the global cursor/selection styles,
    // the drop indicator, and the window Escape listener would all outlive the
    // grid's own listeners being torn down. Also covers unregister racing the
    // POST-release settle window (round-9 #1): drag is already null there, but
    // pendingSettle still tracks the queued commit/glide-back timeout and the
    // cells finishDrag left translated for it — cancel the timeout so the
    // now-torn-down commit handler is never invoked, and strip the transforms
    // immediately since no future FLIP pass will do it for us. Also releases
    // the arbiter token the settle window was holding (round-10 #3) — the
    // timeout body that would normally release it never runs once cleared.
    // Since round-13 #4, pendingSettle also stays alive through the settle
    // timeout's OWN commit interop call, so this same branch covers unregister
    // landing AFTER the timeout already fired and started invokeMethodAsync —
    // window.clearTimeout on an already-fired id is a harmless no-op there; the
    // in-flight promise's own `finally` later finds pendingSettle/settleEls
    // already cleared and releaseGridDrag a no-op (idempotent owner check).
    const cancelActiveDrag = () => {
        nudgeWatch = null;
        clearSortClickSuppressor();
        stopAutoScroll();
        if (drag) { finishDrag(null); return; }
        if (pendingSettle) {
            window.clearTimeout(pendingSettle.timeoutId);
            for (const cell of pendingSettle.cells) clearFlipStyles(cell);
            pendingSettle = null;
            columnReorderSettleEls.delete(gridId);
            releaseGridDrag(gridId, 'column-reorder');
        }
    };

    grid.addEventListener('pointerdown', onPointerDown);
    grid.addEventListener('pointermove', onPointerMove, { passive: false });
    grid.addEventListener('pointerup', onPointerUp);
    grid.addEventListener('pointercancel', onPointerCancel);
    window.addEventListener('pointerup', onWindowPointerUp);
    columnReorderPointerHandlers.set(gridId, {
        grid, onPointerDown, onPointerMove, onPointerUp, onPointerCancel, onWindowPointerUp, cancelActiveDrag,
    });
}

export function unregisterColumnReorder(gridId) {
    const h = columnReorderPointerHandlers.get(gridId);
    if (!h) return;
    h.cancelActiveDrag();
    h.grid.removeEventListener('pointerdown', h.onPointerDown);
    h.grid.removeEventListener('pointermove', h.onPointerMove);
    h.grid.removeEventListener('pointerup', h.onPointerUp);
    h.grid.removeEventListener('pointercancel', h.onPointerCancel);
    window.removeEventListener('pointerup', h.onWindowPointerUp);
    columnReorderPointerHandlers.delete(gridId);
}

// --- DataGrid Row Reorder: unified pointer-based (mouse + touch + pen) ---
//
// Vertical mirror of registerColumnReorder above — same shape (one delegated
// pointer listener per grid, cached base rects, live sibling shift recomputed
// only on projected-index change, glide settle + single commit, Escape/
// pointercancel glide-back, cancelActiveDrag exposed for unregister). Two
// differences from the column engine, both because a row is one draggable
// unit instead of a column's (header + per-row cell) set:
//
//   * ONE element (the <tr>) is translated/shifted, not a per-column cell
//     array — sticky/pinned cells are DOM descendants of the row, so a
//     transform on the <tr> carries them along for free (no separate
//     gather-cells pass needed).
//   * Initiation is handle-only, always (no header-wide/mouse-threshold
//     branch). A row's background is a click-to-select target — column
//     headers can arm from anywhere because their "click" is a scoped
//     sort <button>, but a row has no such safe surface to disambiguate a
//     drag-start from a plain select click. See DataGridRow's grip markup.
//
// Only ever registered for flat, non-virtualized grids — DataGrid.
// RowReorderPointerActive gates both the JS registration below and whether
// DataGridRow renders [data-row-reorder-grip] at all (grouped/tree-grid/
// virtualized grids keep the drag handle visible but inert).

const rowReorderPointerHandlers = new Map(); // gridId -> { grid, onPointerDown, ... }

const ROW_REORDER_SETTLE_MS = 180;  // release/cancel glide duration
const ROW_REORDER_SHIFT_MS = 220;   // sibling live-shift ease duration
const ROW_REORDER_EASE = 'cubic-bezier(0.22, 1, 0.36, 1)';

export function registerRowReorder(gridId, dotnetRef) {
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    if (rowReorderPointerHandlers.has(gridId)) unregisterRowReorder(gridId);

    let drag = null; // active/pending drag descriptor or null
    // Vertical mirror of registerColumnReorder's pendingSettle (round-9 #2):
    // rows/detail <tr>s still carrying an engine-applied transform during the
    // delayed-commit settle window, with the queued timeout that will either
    // fire the commit or glide them back to identity. `drag` is already null
    // by the time that timeout is pending, so unregister-during-settle needs
    // this separate handle to cancel the timeout and strip the transforms.
    let pendingSettle = null; // { timeoutId, els: Set<HTMLElement> } or null

    // Applies (or re-applies) the live sibling-shift preview for a projected
    // insertion index. Guarded by drag.lastProjectedIdx so it only runs when the
    // index actually changes, and only writes rows whose own target transform
    // changed — mirrors applyProjection in registerColumnReorder, translateY
    // instead of translateX.
    const applyProjection = (targetIdx) => {
        if (drag.lastProjectedIdx === targetIdx) return;
        drag.lastProjectedIdx = targetIdx;
        const srcIdx = drag.srcIdx;
        // The gap siblings close is the whole band the dragged row vacates —
        // its own row PLUS its expanded DetailTemplate panel (if any), which
        // moves out as one unit with it (Codex round-4 #3; previously just
        // the parent row's own height, undershooting whenever the dragged
        // row had a detail open).
        const h = drag.sourceBandHeight;
        for (let i = 0; i < drag.rows.length; i++) {
            if (i === srcIdx) continue;
            const row = drag.rows[i];
            let ty = 0;
            if (srcIdx < targetIdx && i > srcIdx && i <= targetIdx) ty = -h;
            else if (srcIdx > targetIdx && i >= targetIdx && i < srcIdx) ty = h;
            if (row.appliedTy === ty) continue;
            row.appliedTy = ty;
            row.el.style.transition = `transform ${ROW_REORDER_SHIFT_MS}ms ${ROW_REORDER_EASE}`;
            row.el.style.transform = ty ? `translateY(${ty}px)` : '';
            // An expanded DetailTemplate's <tr> has no data-row-index of its own
            // (it's absent from drag.rows) but renders as its parent row's very
            // next sibling — carry it along with the exact same transform so it
            // doesn't visually detach from the row it belongs to mid-shift.
            if (row.detail) {
                row.detail.style.transition = row.el.style.transition;
                row.detail.style.transform = row.el.style.transform;
            }
        }
    };

    // Row whose cached base rect contains clientY (clamped to the grid's ends).
    // Never reads layout — baseRect was measured once at drag start.
    const computeTargetIdx = (clientY) => {
        for (let i = 0; i < drag.rows.length; i++) {
            const r = drag.rows[i].baseRect;
            if (clientY >= r.top && clientY <= r.bottom) return i;
            // The vertical gap between this row and the next (if any) is an
            // expanded DetailTemplate panel — it carries no data-row-index so
            // it's absent from drag.rows/baseRect entirely, but it always
            // renders immediately after its OWN parent row. Treat the whole
            // gap as that parent row's target band instead of falling through
            // to the "below everything" fallback below, which used to snap
            // any pointer over a mid-grid detail panel to the LAST row.
            const next = drag.rows[i + 1];
            if (next && clientY > r.bottom && clientY < next.baseRect.top) {
                // finishDrag's RemoveAt(srcIdx)+Insert(targetIdx) treats
                // targetIdx < srcIdx as "insert BEFORE row targetIdx" and
                // targetIdx > srcIdx as "insert AFTER row targetIdx" (see its
                // settle-position comment). A downward drag (srcIdx < i)
                // already gets "after row i" correctly from plain `i` here.
                // An upward drag (srcIdx > i) needs i+1 instead — still <
                // srcIdx, but "insert before row i+1" IS "insert after row
                // i" — so the gap directly below row i's detail band lands
                // there instead of before row i itself, matching what the
                // live sibling-shift preview (applyProjection) shows for
                // this targetIdx (Codex round-5 #1).
                return drag.srcIdx > i ? i + 1 : i;
            }
        }
        const firstR = drag.rows[0].baseRect;
        return clientY < firstR.top ? 0 : drag.rows.length - 1;
    };

    const armDrag = () => {
        drag.armed = true;
        // Saved BEFORE overwriting so finishDrag can restore whatever a
        // consumer's own RowStyle inline declaration had on these four
        // properties, instead of blanking them to '' (Codex round-4 #4).
        drag.savedRowStyle = captureDragStyles(drag.el);
        drag.el.style.transition = 'none';
        drag.el.style.opacity = '0.8';
        drag.el.style.position = 'relative';
        drag.el.style.zIndex = '2';
        drag.el.style.pointerEvents = 'none';
        document.body.style.cursor = 'grabbing';
        document.documentElement.style.cursor = 'grabbing';
        document.body.style.userSelect = 'none';
        try { drag.captureTarget.setPointerCapture(drag.pointerId); } catch (_) { }
        window.addEventListener('keydown', onKeyDown, true);
    };

    const onKeyDown = (e) => {
        if (e.key === 'Escape' && drag) {
            e.preventDefault();
            finishDrag(false);
        }
    };

    // Ends the drag. commit=false is a cancel (Escape / pointercancel / dropped on
    // its own slot) — everything animates back to identity, nothing is committed.
    // For a valid drop, the dragged row glides to its projected slot (siblings are
    // already resting at their live-shift positions); once that settle finishes,
    // OnRowReorderCommit fires exactly once — keyed by stable row identity
    // (data-row-key), not the plain DOM indices measured at drag start. The
    // commit is delayed until AFTER the 180ms settle animation below; if
    // Items/_displayedItems changed underneath that window (server refresh,
    // filter, sort), stale indices would move whatever rows currently occupy
    // those slots instead of the ones the user actually dragged. The keys are
    // read here — synchronously, at drag END, before that window opens — so
    // the .NET side can resolve current indices fresh at commit time, exactly
    // like the column engine already does by id (Codex round-5 #6).
    //
    // Vertical mirror of registerColumnReorder's arbiter hold (round-10 #3):
    // the token claimed at pointerdown is held through the WHOLE settle
    // window, not released here — releasing it immediately let a new
    // column/row reorder or resize claim the same grid while this
    // pendingSettle timeout was still going to invoke .NET, so a stale
    // queued commit could mutate/re-render the grid while another gesture
    // owned live transforms. Released once the settle timeout actually runs
    // (below) or cancelActiveDrag tears down a still-pending settle.
    const finishDrag = (commit) => {
        const d = drag;
        drag = null;
        window.removeEventListener('keydown', onKeyDown, true);
        document.body.style.cursor = '';
        document.documentElement.style.cursor = '';
        document.body.style.userSelect = '';
        if (!d.armed) { releaseGridDrag(gridId, 'row-reorder'); return; } // defensive — handle-only init always arms immediately
        try { d.captureTarget.releasePointerCapture(d.pointerId); } catch (_) { }

        const targetIdx = d.lastProjectedIdx;
        const isCommit = commit && targetIdx !== d.srcIdx;
        const sourceRowKey = d.el.dataset.rowKey;
        const targetRowKey = isCommit ? d.rows[targetIdx].el.dataset.rowKey : null;
        // Pair each shifted row's element with its (possibly absent) detail <tr>
        // so both get the identical settle/reset treatment below — the detail
        // panel was carried along by applyProjection with the same transform,
        // so it must be released the same way.
        const shiftedPairs = d.rows.filter((r) => r.appliedTy).map((r) => [r.el, r.detail]);
        // Vertical mirror of registerColumnReorder's settle fix: MoveRow does a
        // RemoveAt+Insert, so dragging DOWN (target after source) inserts AFTER
        // the target — final top edge is the target's original BOTTOM minus the
        // dragged row's own height (the target already shifted up by that
        // height). Dragging UP inserts BEFORE the target, so its original top
        // edge is already correct. Both the target's bottom and the dragged
        // row's own height must be the FULL band (row + expanded DetailTemplate,
        // if any) — the DOM move relocates that whole band as one unit, and
        // applyProjection already shifted every sibling by d.sourceBandHeight,
        // not just the dragged row's own height (Codex round-4 #3; previously
        // parent-row-only rects here could settle the dragged row into the
        // target's detail panel, or undershoot when the dragged row itself had
        // one open).
        const targetTop = isCommit
            ? (targetIdx > d.srcIdx
                ? (d.rows[targetIdx].baseRect.bottom + d.rows[targetIdx].detailHeight) - d.sourceBandHeight
                : d.rows[targetIdx].baseRect.top)
            : 0;
        const draggedTy = isCommit ? (targetTop - d.sourceRect.top) : 0;

        d.el.style.transition = `transform ${ROW_REORDER_SETTLE_MS}ms ${ROW_REORDER_EASE}`;
        d.el.style.transform = draggedTy ? `translateY(${draggedTy}px)` : '';
        restoreDragStyles(d.el, d.savedRowStyle);
        if (d.detail) {
            // The dragged row's own expanded detail panel rides along with it
            // through the settle glide too.
            d.detail.style.transition = d.el.style.transition;
            d.detail.style.transform = d.el.style.transform;
        }

        if (!isCommit) {
            // Cancel: nothing is going to re-render the grid, so siblings must
            // glide back to identity here too — there's no FLIP pass to hand off to.
            for (const [el, detail] of shiftedPairs) {
                el.style.transition = `transform ${ROW_REORDER_SETTLE_MS}ms ${ROW_REORDER_EASE}`;
                el.style.transform = '';
                if (detail) {
                    detail.style.transition = el.style.transition;
                    detail.style.transform = '';
                }
            }
        }

        const settleEls = new Set([d.el, d.detail, ...shiftedPairs.flat()].filter(Boolean));
        // Recorded by element reference (round-11 #1) — see rowReorderSettleEls.
        rowReorderSettleEls.set(gridId, settleEls);
        const timeoutId = window.setTimeout(() => {
            d.el.style.transition = '';
            if (d.detail) d.detail.style.transition = '';
            if (isCommit) {
                // Leave the settled transform in place (dragged row at its
                // projected slot, siblings at their live-shift positions) —
                // captureRowRects measures this true final visual order next
                // and clears these inline styles itself.
                //
                // Vertical mirror of registerColumnReorder's fix (round-13 #4):
                // pendingSettle stays set (NOT nulled here) through the commit
                // interop call itself. The old code nulled it synchronously the
                // instant this timeout fired, before invokeMethodAsync even
                // started — if unregisterRowReorder raced that in-flight window
                // (RowReorderable flipped off mid-commit, plain Blazor Server
                // latency), cancelActiveDrag saw pendingSettle === null and
                // skipped its cleanup branch entirely, even though
                // rowReorderSettleEls still held these exact elements: nothing
                // would ever strip their transforms, since no future FLIP pass
                // was coming (captureRowRects/clearRowReorderTransforms — which
                // own rowReorderSettleEls's normal lifecycle, deleting it
                // themselves — are only reached from a commit that actually
                // lands, which a torn-down component will never receive).
                // pendingSettle is only cleared in this promise's `finally`
                // (rowReorderSettleEls is deliberately left untouched here — it
                // stays exactly whichever of captureRowRects/
                // clearRowReorderTransforms's job it already was), so a
                // cancelActiveDrag landing mid-flight still finds pendingSettle
                // and strips the transforms itself. The token is released from
                // the SAME `finally`, not synchronously right after
                // invokeMethodAsync starts (round-12 #2) — a slow awaited
                // OnRowReorder consumer handler would otherwise let another
                // gesture claim the grid while the commit is still in flight.
                // `finally` covers a rejected commit too.
                dotnetRef.invokeMethodAsync('OnRowReorderCommit', gridId, sourceRowKey, targetRowKey)
                    .finally(() => {
                        pendingSettle = null;
                        releaseGridDrag(gridId, 'row-reorder');
                    });
            } else {
                for (const [el, detail] of shiftedPairs) {
                    el.style.transition = '';
                    el.style.transform = '';
                    if (detail) {
                        detail.style.transition = '';
                        detail.style.transform = '';
                    }
                }
                // No commit fires on cancel — nothing to await, release now.
                pendingSettle = null;
                releaseGridDrag(gridId, 'row-reorder');
            }
        }, ROW_REORDER_SETTLE_MS + 20);
        pendingSettle = { timeoutId, els: settleEls };
    };

    const onPointerMove = (e) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        if (e.cancelable) e.preventDefault();
        // Translate the dragged row in Y only, clamped to the grid's row span.
        let ty = e.clientY - drag.startY;
        const minTy = drag.bounds.min - drag.sourceRect.top;
        const maxTy = drag.bounds.max - drag.sourceRect.bottom;
        if (ty < minTy) ty = minTy;
        if (ty > maxTy) ty = maxTy;
        const translate = ty ? `translateY(${ty}px)` : '';
        drag.el.style.transform = translate;
        // The dragged row's own expanded detail panel (if any) has to move
        // WITH it — it's a separate <tr> the drag never captured/translated.
        if (drag.detail) drag.detail.style.transform = translate;

        applyProjection(computeTargetIdx(e.clientY));
    };

    const onPointerUp = (e) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        finishDrag(true);
    };

    const onPointerCancel = (e) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        finishDrag(false);
    };

    // A row's own <tr> is immediately followed by its expanded DetailTemplate's
    // <tr> (rendered as a sibling by DataGridRow) when one is open — that detail
    // row carries no data-row-index of its own, so it's found by position, not
    // by attribute.
    const detailRowFor = (tr) => {
        const next = tr.nextElementSibling;
        return (next && next.tagName === 'TR' && !next.hasAttribute('data-row-index')) ? next : null;
    };

    const onPointerDown = (e) => {
        // Ignore non-primary mouse buttons; touch/pen always fall through —
        // mirrors registerColumnResize's guard. This grip arms IMMEDIATELY
        // (no movement-threshold/header-wide path to filter it later like
        // registerColumnReorder's `e.buttons & 1` re-check), so without this a
        // right- or middle-button press on the grip would still preventDefault
        // (suppressing the row's context menu) and could commit a reorder on
        // release (round-8 #5).
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        // A second pointerdown while a row drag is already live (second touch
        // point, second mouse button, concurrent pen) must not overwrite the
        // single `drag` descriptor — mirrors registerColumnReorder's guard
        // (Codex round-5 #3).
        if (drag) return;
        // Handle-only initiation — see the section comment above for why rows
        // don't get a header-wide/movement-threshold arm path like columns do.
        const grip = e.target.closest('[data-row-reorder-grip]');
        if (!grip || !grid.contains(grip)) return;
        // A DataGrid nested inside another row-reorderable grid's DetailTemplate
        // or cell template sits inside this grid's DOM subtree too, so
        // grid.contains(grip) alone can't tell the two apart — require the
        // grip's OWN closest grid root to be this grid, not just any ancestor
        // of it, or the outer grid would also arm on the inner grid's grips and
        // commit with the inner row's indices (Codex round-3 #5).
        if (grip.closest('[data-grid-id]') !== grid) return;
        const tr = grip.closest('tr[data-row-index]');
        if (!tr) return;
        const tbody = tr.closest('tbody');
        if (!tbody) return;

        // Every reorderable row in the tbody, with cached base rects — measured
        // ONCE here so the live sibling-shift preview never reads layout in the
        // pointermove loop. data-row-index rows are exactly this grid's data
        // rows (group headers / detail rows carry no such attribute), and
        // RowReorderPointerActive guarantees a flat, non-virtualized body, so
        // every reorderable row is present in the DOM and this index equals its
        // _displayedItems index 1:1.
        // A row-reorderable grid nested inside THIS grid's DetailTemplate/cell
        // template also has tr[data-row-index] rows that are descendants of
        // this tbody — filter to rows this grid actually owns, or the inner
        // grid's rows would be sorted into `rows` and a drag could commit
        // against the wrong _displayedItems index (Codex round-4 #2).
        const allRows = ownedByGrid(tbody.querySelectorAll('tr[data-row-index]'), grid);
        if (allRows.length < 2) return;

        const rows = allRows.map((el) => {
            const detail = detailRowFor(el);
            return {
                el, idx: Number(el.dataset.rowIndex), baseRect: el.getBoundingClientRect(), appliedTy: 0,
                detail, detailHeight: detail ? detail.getBoundingClientRect().height : 0,
            };
        });
        rows.sort((a, b) => a.idx - b.idx);
        const srcIdx = rows.findIndex((r) => r.el === tr);
        if (srcIdx < 0) return;

        const sourceRect = tr.getBoundingClientRect();
        const bounds = {
            min: rows[0].baseRect.top,
            max: rows[rows.length - 1].baseRect.bottom,
        };

        // Cross-engine: a resize (or column-reorder) already live on this
        // grid owns the shared arbiter token — refuse to start a competing
        // row reorder (round-6 finding; see the arbiter comment above
        // registerColumnResize). Claimed here, right before `drag` becomes
        // non-null, so a rejected claim never touches drag state.
        if (!claimGridDrag(gridId, 'row-reorder')) return;

        drag = {
            pointerId: e.pointerId, captureTarget: grip,
            startY: e.clientY, el: tr, detail: rows[srcIdx].detail, rows, srcIdx, sourceRect, bounds,
            // Total band height the dragged row vacates — its own row plus
            // its own expanded detail panel, if any (Codex round-4 #3).
            sourceBandHeight: sourceRect.height + rows[srcIdx].detailHeight,
            lastProjectedIdx: srcIdx, armed: false,
        };

        armDrag();
        e.preventDefault();
        e.stopPropagation();
    };

    // Exposed so unregisterRowReorder can fully abort an in-flight drag on
    // unmount — otherwise translated rows, the global cursor/selection styles,
    // and the window Escape listener would all outlive the grid's own
    // listeners being torn down. Also covers unregister racing the POST-release
    // settle window (round-9 #2): drag is already null there, but pendingSettle
    // still tracks the queued commit/glide-back timeout and the row/detail
    // elements finishDrag left translated for it — cancel the timeout so the
    // now-torn-down commit handler is never invoked, and strip the transforms
    // immediately since no future FLIP pass will do it for us. Also releases
    // the arbiter token the settle window was holding (round-10 #3) — the
    // timeout body that would normally release it never runs once cleared.
    // Since round-13 #4, pendingSettle also stays alive through the settle
    // timeout's OWN commit interop call (mirrors registerColumnReorder's
    // cancelActiveDrag — see its remarks), so this same branch covers
    // unregister landing AFTER the timeout already fired and started
    // invokeMethodAsync.
    const cancelActiveDrag = () => {
        if (drag) { finishDrag(false); return; }
        if (pendingSettle) {
            window.clearTimeout(pendingSettle.timeoutId);
            for (const el of pendingSettle.els) clearFlipStyles(el);
            pendingSettle = null;
            rowReorderSettleEls.delete(gridId);
            releaseGridDrag(gridId, 'row-reorder');
        }
    };

    grid.addEventListener('pointerdown', onPointerDown);
    grid.addEventListener('pointermove', onPointerMove, { passive: false });
    grid.addEventListener('pointerup', onPointerUp);
    grid.addEventListener('pointercancel', onPointerCancel);
    rowReorderPointerHandlers.set(gridId, {
        grid, onPointerDown, onPointerMove, onPointerUp, onPointerCancel, cancelActiveDrag,
    });
}

export function unregisterRowReorder(gridId) {
    const h = rowReorderPointerHandlers.get(gridId);
    if (!h) return;
    h.cancelActiveDrag();
    h.grid.removeEventListener('pointerdown', h.onPointerDown);
    h.grid.removeEventListener('pointermove', h.onPointerMove);
    h.grid.removeEventListener('pointerup', h.onPointerUp);
    h.grid.removeEventListener('pointercancel', h.onPointerCancel);
    rowReorderPointerHandlers.delete(gridId);
}

// --- DataGrid Row Reorder FLIP Animation ---
//
// Same First-Last-Invert-Play handshake as captureColumnRects/animateColumnReorder,
// keyed by a stable per-row identity (data-row-key, set from DataGridRowKeys — the
// same value Blazor's own @key uses) instead of index, because the row that moved
// is exactly the one whose index is no longer trustworthy across the mutation.

const rowReorderSnapshots = new Map(); // gridId -> Map<rowKey, top>
const rowReorderInFlight = new Map();  // gridId -> Set<HTMLElement>
// Row-engine mirror of columnReorderSettleEls (round-11 #1) — gridId ->
// Set<HTMLElement> holding a JS-authored settle transform for a commit .NET
// hasn't resolved yet, by element reference so clearRowReorderTransforms
// still finds them after a rerender strips data-row-index.
const rowReorderSettleEls = new Map();

// Clears ONLY the inline properties the FLIP engine itself ever sets
// (transform/transition during the live-shift preview + settle glide,
// willChange during animateRowReorder) — never opacity/zIndex/position/
// pointerEvents. Those four are the pointer-drag engine's own concern (set
// by armDrag, already reset by finishDrag for the one row it touched) and
// are never written by captureRowRects/animateRowReorder for ANY row,
// dragged or sibling. Clearing them here used to also wipe out consumer
// RowStyle inline declarations (opacity, position, pointer-events, ...) on
// every row this FLIP pass touched, since Blazor's diff skips the style
// attribute when nothing it tracks changed, leaving JS the only thing that
// ever un-sets an inline property it didn't itself set (Codex round-3 #3).
function clearRowFlipStyles(tr) {
    tr.style.transform = '';
    tr.style.transition = '';
    tr.style.willChange = '';
}

// Standalone mirror of registerRowReorder's detailRowFor() closure — needed
// here too since this capture/animate pass runs independently of that
// closure (invoked from DataGrid's commit handler, not the pointer engine).
const rowDetailSibling = (tr) => {
    const next = tr.nextElementSibling;
    return (next && next.tagName === 'TR' && !next.hasAttribute('data-row-index')) ? next : null;
};

export function captureRowRects(gridId) {
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    // If a previous FLIP for this grid is still mid-flight, force it back to
    // identity BEFORE measuring — same rationale as captureColumnRects.
    const inFlight = rowReorderInFlight.get(gridId);
    if (inFlight) {
        for (const tr of inFlight) clearRowFlipStyles(tr);
        rowReorderInFlight.delete(gridId);
    }
    // Accept path settling successfully — drop the settle-els reference set
    // from finishDrag (see rowReorderSettleEls) so a later drag's reject
    // cleanup never acts on this drag's now-stale elements.
    rowReorderSettleEls.delete(gridId);
    // A row-reorderable grid nested inside THIS grid's DetailTemplate/cell
    // template also matches 'tbody tr[data-row-index]' from this grid's own
    // root — exclude rows that actually belong to that inner grid (Codex
    // round-4 #2).
    const rows = ownedByGrid(grid.querySelectorAll('tbody tr[data-row-index]'), grid);
    if (!rows.length) return;
    const snapshot = new Map();
    rows.forEach((tr) => {
        const rowKey = tr.dataset.rowKey;
        if (!rowKey) return;
        // Measure BEFORE clearing: the pointer engine leaves the dragged row's
        // (and its live-shifted siblings') transforms in place through commit,
        // so this "top" is the accurate settled preview position.
        const top = tr.getBoundingClientRect().top;
        clearRowFlipStyles(tr);
        // finishDrag intentionally leaves a translateY on the dragged row's
        // (and any shifted sibling's) expanded DetailTemplate <tr> through
        // commit for this FLIP handoff — it carries no data-row-index of its
        // own so the query above never visits it directly; clear it here too
        // or the offset survives the Blazor reorder (Codex round-4 #7).
        const detail = rowDetailSibling(tr);
        if (detail) clearRowFlipStyles(detail);
        snapshot.set(rowKey, top);
    });
    rowReorderSnapshots.set(gridId, snapshot);
}

// Snaps every row (and any expanded detail sibling) back to identity when a
// delayed row-reorder commit is REJECTED client-side (round-8 #2) — e.g. the
// backing rows changed during the 180ms settle window so ReorderRowByKeyAsync's
// source/target key resolution fails, and it never reaches captureRowRects.
// registerRowReorder's finishDrag already left the dragged row and every
// live-shifted sibling (plus their detail rows) translated for that call to
// clear as part of the FLIP handoff; on rejection nothing else will, so this is
// the dedicated no-animation counterpart. No transition is (re)applied —
// finishDrag's own settle glide already finished visually before the
// (now-rejected) commit was even dispatched, so replaying a second glide here
// would misleadingly look like an accepted move.
export function clearRowReorderTransforms(gridId) {
    // Element-reference cleanup FIRST (round-11 #1): the rejecting rerender
    // (grouped/virtualized/RowReorderable toggled off) can strip
    // data-row-index from the exact rows finishDrag left transformed BEFORE
    // this runs — the attribute-based sweep below then matches nothing and
    // the JS-authored transforms (which Blazor never clears) stay applied.
    // These are the same DOM nodes finishDrag touched, found by reference,
    // immune to whatever attribute the rerender removed.
    const settleEls = rowReorderSettleEls.get(gridId);
    if (settleEls) {
        for (const tr of settleEls) clearRowFlipStyles(tr);
        rowReorderSettleEls.delete(gridId);
    }
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    const inFlight = rowReorderInFlight.get(gridId);
    if (inFlight) {
        for (const tr of inFlight) clearRowFlipStyles(tr);
        rowReorderInFlight.delete(gridId);
    }
    rowReorderSnapshots.delete(gridId);
    const rows = ownedByGrid(grid.querySelectorAll('tbody tr[data-row-index]'), grid);
    rows.forEach((tr) => {
        clearRowFlipStyles(tr);
        const detail = rowDetailSibling(tr);
        if (detail) clearRowFlipStyles(detail);
    });
}

export function animateRowReorder(gridId, durationMs) {
    const snapshot = rowReorderSnapshots.get(gridId);
    rowReorderSnapshots.delete(gridId);
    if (!snapshot) return;
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    const duration = Number(durationMs) > 0 ? Number(durationMs) : 200;

    const rows = ownedByGrid(grid.querySelectorAll('tbody tr[data-row-index]'), grid);
    const inFlight = new Set();
    rows.forEach((tr) => {
        const rowKey = tr.dataset.rowKey;
        const oldTop = snapshot.get(rowKey);
        if (oldTop === undefined) return;
        const newTop = tr.getBoundingClientRect().top;
        const delta = oldTop - newTop;
        // Even when the row itself didn't move, its detail sibling (if any)
        // may still carry a stale live-shift/settle transform of its own
        // from before the render (Codex round-4 #7) — clear it before
        // bailing out of this row's animation.
        const detail = rowDetailSibling(tr);
        if (Math.abs(delta) < 1) {
            if (detail) clearRowFlipStyles(detail);
            return;
        }

        tr.style.transition = 'none';
        tr.style.transform = `translateY(${delta}px)`;
        tr.style.willChange = 'transform';
        inFlight.add(tr);
        // The detail panel has no independent row-key/FLIP tracking of its
        // own, but it must ride the identical inverse-then-release animation
        // as its parent or it visually detaches from it (Codex round-4 #7).
        if (detail) {
            detail.style.transition = 'none';
            detail.style.transform = `translateY(${delta}px)`;
            detail.style.willChange = 'transform';
            inFlight.add(detail);
        }
    });
    if (inFlight.size === 0) return;
    // Force layout flush so the inverse transform is registered before the
    // transition is kicked off — reading offsetHeight from any one row suffices.
    // eslint-disable-next-line no-unused-expressions
    grid.offsetHeight;
    for (const tr of inFlight) {
        tr.style.transition = `transform ${duration}ms cubic-bezier(0.22, 1, 0.36, 1)`;
        tr.style.transform = '';
    }
    rowReorderInFlight.set(gridId, inFlight);
    window.setTimeout(() => {
        const owned = rowReorderInFlight.get(gridId);
        if (owned !== inFlight) return;
        for (const tr of inFlight) clearRowFlipStyles(tr);
        rowReorderInFlight.delete(gridId);
    }, duration + 50);
}

// --- File Download ---

export function downloadFile(fileName, contentBase64, mimeType) {
    const a = document.createElement('a');
    a.href = `data:${mimeType || 'application/octet-stream'};base64,${contentBase64}`;
    a.download = fileName;
    a.click();
}

// --- Clipboard ---

export async function copyToClipboard(text) {
    await navigator.clipboard.writeText(text);
}

// --- Tour: get element rect by CSS selector ---

export function getElementRectBySelector(selector) {
    const el = document.querySelector(selector);
    if (!el) return null;
    const rect = el.getBoundingClientRect();
    // Also surface the computed border-radius so the tour spotlight can match the target's corner shape.
    const radiusStr = getComputedStyle(el).borderTopLeftRadius || '0';
    const radius = parseFloat(radiusStr) || 0;
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height, borderRadius: radius };
}

export function scrollSelectorIntoView(selector) {
    const el = document.querySelector(selector);
    if (!el) return;
    // 'instant' on purpose: the caller measures the rect right after this
    // call — a smooth scroll would still be mid-animation when measured.
    el.scrollIntoView({ behavior: 'instant', block: 'center', inline: 'nearest' });
}

// Scrolls an element (by id) into view inside its nearest scroll container.
// Used by keyboard-navigated lists (Command palette active item) to keep the
// highlighted row visible as Arrow/Home/End move it. block: 'nearest' avoids
// yanking the whole list when the row is already visible; only off-screen rows
// scroll. No-op when the element is absent (e.g. filtered out this render).
export function scrollIntoViewById(elementId, block) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.scrollIntoView({ behavior: 'instant', block: block || 'nearest', inline: 'nearest' });
}

// --- Affix: scroll-based sticky positioning ---

const affixHandlers = new Map();

export function registerAffix(elementId, offsetTop, offsetBottom, targetSelector, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    const scrollTarget = targetSelector ? document.querySelector(targetSelector) : window;
    if (!scrollTarget) return;

    const placeholder = document.createElement('div');
    placeholder.style.display = 'none';
    let isFixed = false;

    // While affixed, the element is position:fixed with an inline width frozen
    // at the moment it stuck. The placeholder still occupies the element's slot
    // in normal flow, so it reflows with the parent on a window resize / device
    // rotation. Re-sync the fixed element's width (and the placeholder's frozen
    // box) to the live placeholder geometry so a responsive affixed bar tracks
    // its container instead of staying stale at its first-render width.
    const syncFixedWidth = () => {
        if (!isFixed) return;
        // Reading el.offsetWidth while fixed returns the frozen width; the
        // placeholder is the in-flow proxy, so measure it instead. Temporarily
        // drop the recorded width so the placeholder reflows to its natural
        // (parent-driven) size before we re-read it.
        placeholder.style.width = '';
        const naturalWidth = placeholder.getBoundingClientRect().width;
        if (naturalWidth > 0) {
            placeholder.style.width = naturalWidth + 'px';
            el.style.width = naturalWidth + 'px';
        }
    };

    const onScroll = () => {
        const rect = (isFixed ? placeholder : el).getBoundingClientRect();

        if (offsetBottom != null) {
            const viewportHeight = window.innerHeight;
            if (!isFixed && rect.bottom >= viewportHeight - offsetBottom) {
                isFixed = true;
                const elRect = el.getBoundingClientRect();
                el.parentNode.insertBefore(placeholder, el);
                placeholder.style.display = 'block';
                placeholder.style.height = elRect.height + 'px';
                placeholder.style.width = elRect.width + 'px';
                el.style.position = 'fixed';
                el.style.bottom = offsetBottom + 'px';
                el.style.width = elRect.width + 'px';
                el.style.zIndex = '40';
                dotnetRef.invokeMethodAsync('OnAffixChanged', elementId, true);
            } else if (isFixed) {
                const placeholderRect = placeholder.getBoundingClientRect();
                if (placeholderRect.bottom < viewportHeight - offsetBottom) {
                    isFixed = false;
                    el.style.position = '';
                    el.style.bottom = '';
                    el.style.width = '';
                    el.style.zIndex = '';
                    placeholder.style.display = 'none';
                    dotnetRef.invokeMethodAsync('OnAffixChanged', elementId, false);
                }
            }
        } else {
            if (!isFixed && rect.top <= offsetTop) {
                isFixed = true;
                const elRect = el.getBoundingClientRect();
                el.parentNode.insertBefore(placeholder, el);
                placeholder.style.display = 'block';
                placeholder.style.height = elRect.height + 'px';
                placeholder.style.width = elRect.width + 'px';
                el.style.position = 'fixed';
                el.style.top = offsetTop + 'px';
                el.style.width = elRect.width + 'px';
                el.style.zIndex = '40';
                dotnetRef.invokeMethodAsync('OnAffixChanged', elementId, true);
            } else if (isFixed && placeholder.getBoundingClientRect().top > offsetTop) {
                isFixed = false;
                el.style.position = '';
                el.style.top = '';
                el.style.width = '';
                el.style.zIndex = '';
                placeholder.style.display = 'none';
                dotnetRef.invokeMethodAsync('OnAffixChanged', elementId, false);
            }
        }
    };

    // On resize/rotate, first re-measure the affixed width from the reflowed
    // placeholder, then re-evaluate the stick/unstick boundary (the viewport
    // height the offsetBottom branch compares against also changes).
    const onResize = () => {
        syncFixedWidth();
        onScroll();
    };

    const eventTarget = scrollTarget === window ? window : scrollTarget;
    eventTarget.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onResize, { passive: true });
    affixHandlers.set(elementId, { onScroll, onResize, placeholder, eventTarget });

    // Initial check
    requestAnimationFrame(onScroll);
}

export function unregisterAffix(elementId) {
    const handler = affixHandlers.get(elementId);
    if (handler) {
        handler.eventTarget.removeEventListener('scroll', handler.onScroll);
        window.removeEventListener('resize', handler.onResize);
        if (handler.placeholder.parentNode) handler.placeholder.remove();
        const el = document.getElementById(elementId);
        if (el) {
            el.style.position = '';
            el.style.top = '';
            el.style.bottom = '';
            el.style.width = '';
            el.style.zIndex = '';
        }
        affixHandlers.delete(elementId);
    }
}

// --- BackToTop: scroll detection ---

const backToTopHandlers = new Map();

export function registerBackToTop(id, dotnetRef, threshold, target) {
    // Clean up previous registration for this id (detach from whatever scroll
    // source the previous registration was bound to, not necessarily window).
    if (backToTopHandlers.has(id)) {
        const prev = backToTopHandlers.get(id);
        prev.scrollSource.removeEventListener('scroll', prev.handler);
    }

    // When a Target selector is supplied, observe that container's scrollTop and
    // listen for scroll on it; otherwise fall back to the window/document. A
    // selector that doesn't resolve also falls back to window so registration
    // never throws on a stale/typo'd Target. (#98)
    const container = target ? document.querySelector(target) : null;
    const scrollSource = container || window;

    const effectiveThreshold = threshold || 300;
    // Throttle to one check per animation frame — a raw scroll handler fires
    // dozens of times per gesture and each call crosses the JS<->.NET interop
    // boundary. We also only invoke when the visibility actually flips. (#247)
    let rafPending = false;
    let lastVisible = null;
    const compute = () => {
        rafPending = false;
        const scrollY = container
            ? container.scrollTop
            : (window.scrollY || document.documentElement.scrollTop);
        const visible = scrollY > effectiveThreshold;
        if (visible === lastVisible) return;
        lastVisible = visible;
        dotnetRef.invokeMethodAsync('OnScrollVisibilityChanged', id, visible);
    };
    const handler = () => {
        if (rafPending) return;
        rafPending = true;
        requestAnimationFrame(compute);
    };

    scrollSource.addEventListener('scroll', handler, { passive: true });
    backToTopHandlers.set(id, { handler, dotnetRef, scrollSource, target: target || null });
    compute(); // initial check (synchronous)
}

export function unregisterBackToTop(id) {
    const entry = backToTopHandlers.get(id);
    if (entry) {
        entry.scrollSource.removeEventListener('scroll', entry.handler);
        backToTopHandlers.delete(id);
    }
}

export function scrollToTop(target) {
    // Scroll the targeted container back to the top when a selector is given,
    // otherwise scroll the window. A stale/unresolved selector falls back to
    // the window so the button never silently no-ops. (#98)
    const container = target ? document.querySelector(target) : null;
    (container || window).scrollTo({ top: 0, behavior: 'smooth' });
}

// --- InputMask: read / restore a text input's caret (selectionStart) ---
// Used by InputMask to keep the caret put after re-masking: getInputCaret reads
// it before the value is rewritten, setInputCaret restores it (collapsed) after.

export function getInputCaret(elementId) {
    const el = document.getElementById(elementId);
    if (!el || typeof el.selectionStart !== 'number') return 0;
    return el.selectionStart;
}

export function setInputCaret(elementId, position) {
    const el = document.getElementById(elementId);
    if (!el || typeof el.setSelectionRange !== 'function') return;
    const len = (el.value || '').length;
    const pos = Math.max(0, Math.min(position, len));
    try { el.setSelectionRange(pos, pos); } catch { /* element not focusable yet */ }
}

// Force the live DOM value of a masked <input> when Blazor's diff won't patch it
// (the re-masked display equals the previous render after a rejected char, #41).
export function setInputValue(elementId, value) {
    const el = document.getElementById(elementId);
    if (!el) return;
    if (el.value !== value) el.value = value;
}

// --- Mention: get textarea caret coordinates ---

export function getTextareaCaretPosition(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return { top: 0, left: 0, offsetTop: 0, offsetLeft: 0, lineHeight: 20, selectionStart: 0 };

    const { selectionStart } = el;
    const elRect = el.getBoundingClientRect();

    // Create mirror div to measure caret position
    const div = document.createElement('div');
    const style = getComputedStyle(el);
    div.style.cssText = [
        'position:absolute', 'visibility:hidden', 'white-space:pre-wrap', 'word-wrap:break-word',
        `width:${style.width}`, `font:${style.font}`, `padding:${style.padding}`,
        `border:${style.border}`, `line-height:${style.lineHeight}`,
        `letter-spacing:${style.letterSpacing}`, `box-sizing:${style.boxSizing}`
    ].join(';');
    div.textContent = el.value.substring(0, selectionStart);
    const span = document.createElement('span');
    span.textContent = '\u200b';
    div.appendChild(span);
    document.body.appendChild(div);

    // Viewport-relative coordinates (kept for back-compat with any caller).
    const top = elRect.top + span.offsetTop - el.scrollTop;
    const left = elRect.left + span.offsetLeft - el.scrollLeft;

    // Caret position relative to the textarea's offsetParent (the Mention
    // component's `position: relative` wrapper). The dropdown is positioned
    // absolutely against that wrapper so it tracks the textarea on page /
    // container scroll instead of staying pinned at a stale viewport point
    // (#205). offsetTop/Left already account for the textarea's own position
    // within the wrapper.
    const caretTop = el.offsetTop + span.offsetTop - el.scrollTop;
    const caretLeft = el.offsetLeft + span.offsetLeft - el.scrollLeft;
    const lineHeight = parseFloat(style.lineHeight) || (parseFloat(style.fontSize) * 1.2) || 20;

    document.body.removeChild(div);

    return { top, left, offsetTop: caretTop, offsetLeft: caretLeft, lineHeight, selectionStart };
}

// --- LocalStorage ---

export function saveToLocalStorage(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch (e) {
        // Quota exceeded or private browsing — silently ignore
    }
}

export function loadFromLocalStorage(key) {
    try {
        return localStorage.getItem(key);
    } catch (e) {
        return null;
    }
}

export function removeFromLocalStorage(key) {
    try {
        localStorage.removeItem(key);
    } catch (e) {
        // ignore
    }
}

// --- ColorPicker SV Drag ---

const svDragHandlers = new Map();

export function registerSvDrag(elementId, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    let dragging = false;
    let pointerId = null;

    const compute = (e) => {
        const rect = el.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return;
        const x = Math.max(0, Math.min(rect.width, e.clientX - rect.left));
        const y = Math.max(0, Math.min(rect.height, e.clientY - rect.top));
        const s = (x / rect.width) * 100;
        const v = (1 - y / rect.height) * 100;
        dotnetRef.invokeMethodAsync('OnSvDrag', elementId, s, v);
    };

    const onPointerDown = (e) => {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        dragging = true;
        pointerId = e.pointerId;
        try { el.setPointerCapture(e.pointerId); } catch (_) { }
        compute(e);
        e.preventDefault();
    };
    const onPointerMove = (e) => {
        if (!dragging || e.pointerId !== pointerId) return;
        compute(e);
    };
    const onPointerUp = (e) => {
        if (!dragging || e.pointerId !== pointerId) return;
        dragging = false;
        try { el.releasePointerCapture(e.pointerId); } catch (_) { }
        pointerId = null;
    };

    el.addEventListener('pointerdown', onPointerDown);
    el.addEventListener('pointermove', onPointerMove);
    el.addEventListener('pointerup', onPointerUp);
    el.addEventListener('pointercancel', onPointerUp);

    svDragHandlers.set(elementId, { el, onPointerDown, onPointerMove, onPointerUp });
}

export function unregisterSvDrag(elementId) {
    const h = svDragHandlers.get(elementId);
    if (h && h.el) {
        h.el.removeEventListener('pointerdown', h.onPointerDown);
        h.el.removeEventListener('pointermove', h.onPointerMove);
        h.el.removeEventListener('pointerup', h.onPointerUp);
        h.el.removeEventListener('pointercancel', h.onPointerUp);
    }
    svDragHandlers.delete(elementId);
}

// --- OnThisPage (docs TOC) ---

const onThisPageObservers = new Map();

export function onThisPageScan(containerSelector) {
    const container = document.querySelector(containerSelector);
    if (!container) return [];
    // Pick up classical headings AND explicitly-tagged TOC entries (e.g. ComponentDemo sections).
    // Preserve DOM order so the TOC reads top-to-bottom.
    const nodes = container.querySelectorAll('h2[id], h3[id], [data-toc-entry][id]');
    return Array.from(nodes).map(h => {
        const tocTitle = h.getAttribute('data-toc-title');
        const isDemo = h.hasAttribute('data-toc-entry');
        return {
            id: h.id,
            text: (tocTitle || h.textContent || '').trim(),
            level: isDemo ? 3 : parseInt(h.tagName.substring(1), 10)
        };
    });
}

export function onThisPageObserve(id, containerSelector, dotNetRef) {
    const container = document.querySelector(containerSelector);
    if (!container) return;
    const nodes = container.querySelectorAll('h2[id], h3[id], [data-toc-entry][id]');
    if (nodes.length === 0) return;

    let currentActive = null;
    const visibleSet = new Set();

    const update = () => {
        if (visibleSet.size === 0) return;
        // Pick the heading nearest the top of the viewport
        let best = null;
        let bestTop = Infinity;
        visibleSet.forEach(el => {
            const top = el.getBoundingClientRect().top;
            if (top < bestTop) { bestTop = top; best = el; }
        });
        if (best && best.id !== currentActive) {
            currentActive = best.id;
            dotNetRef.invokeMethodAsync('SetActive', currentActive);
        }
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) visibleSet.add(e.target);
            else visibleSet.delete(e.target);
        });
        update();
    }, {
        // Highlight when a heading is in the top portion of the viewport
        rootMargin: '-88px 0px -70% 0px',
        threshold: 0
    });

    nodes.forEach(h => observer.observe(h));
    onThisPageObservers.set(id, observer);

    // If nothing is in the observed band on load, default to the first heading
    if (nodes.length > 0) {
        dotNetRef.invokeMethodAsync('SetActive', nodes[0].id);
    }
}

export function onThisPageUnobserve(id) {
    const obs = onThisPageObservers.get(id);
    if (obs) {
        obs.disconnect();
        onThisPageObservers.delete(id);
    }
}

// --- Sortable Touch (rc.44) ---
//
// HTML5 Drag-and-Drop events (dragstart/dragover/drop) DO NOT fire on touch
// devices — confirmed across iOS Safari, Android Chrome, and mobile Firefox.
// SortableList therefore needs a parallel touch-based path. We track the
// finger via touchstart/touchmove/touchend, use document.elementFromPoint to
// determine the drop target, and round-trip a single index pair back to
// Blazor via the service's [JSInvokable] OnSortableTouchDrop callback,
// which routes to the registered component handler.
//
// Mouse keeps using the existing HTML5 path — we only activate on touch
// pointers (touchstart fires on every touch, never on mouse).

const sortableTouchHandlers = new Map();

export function registerSortableTouch(containerId, dotnetRef) {
    const container = document.getElementById(containerId);
    if (!container) return;

    // Clean up any prior registration on the same container.
    const prev = sortableTouchHandlers.get(containerId);
    if (prev) {
        container.removeEventListener('touchstart', prev.onTouchStart);
        container.removeEventListener('touchmove', prev.onTouchMove);
        container.removeEventListener('touchend', prev.onTouchEnd);
        container.removeEventListener('touchcancel', prev.onTouchEnd);
    }

    let sourceIndex = -1;
    let lastTargetIndex = -1;
    let draggingEl = null;
    let lastHighlightEl = null;

    const findItem = (target) => {
        if (!target || typeof target.closest !== 'function') return null;
        return target.closest('[data-sortable-item]');
    };

    const indexOf = (item) => {
        if (!item) return -1;
        const v = parseInt(item.dataset.sortableIndex, 10);
        return Number.isFinite(v) ? v : -1;
    };

    const clearHighlight = () => {
        if (lastHighlightEl) {
            lastHighlightEl.removeAttribute('data-sortable-over');
            lastHighlightEl = null;
        }
    };

    const onTouchStart = (e) => {
        if (e.touches.length !== 1) return;
        const item = findItem(e.target);
        if (!item || !container.contains(item)) return;
        const idx = indexOf(item);
        if (idx < 0) return;
        sourceIndex = idx;
        lastTargetIndex = idx;
        draggingEl = item;
        item.setAttribute('data-sortable-dragging', '');
    };

    const onTouchMove = (e) => {
        if (sourceIndex < 0 || e.touches.length !== 1) return;
        const t = e.touches[0];
        // Prevent the page scrolling while reordering — same UX as desktop.
        // We must NOT use { passive: true } on touchmove for this to work.
        if (e.cancelable) e.preventDefault();
        const under = document.elementFromPoint(t.clientX, t.clientY);
        const item = findItem(under);
        if (!item || !container.contains(item)) return;
        const idx = indexOf(item);
        if (idx < 0 || idx === lastTargetIndex) return;
        clearHighlight();
        item.setAttribute('data-sortable-over', '');
        lastHighlightEl = item;
        lastTargetIndex = idx;
    };

    const onTouchEnd = () => {
        const src = sourceIndex;
        const tgt = lastTargetIndex;
        // Reset state before the round-trip so a re-render finds a clean DOM.
        if (draggingEl) draggingEl.removeAttribute('data-sortable-dragging');
        clearHighlight();
        sourceIndex = -1;
        lastTargetIndex = -1;
        draggingEl = null;
        if (src >= 0 && tgt >= 0 && src !== tgt) {
            dotnetRef.invokeMethodAsync('OnSortableTouchDrop', containerId, src, tgt)
                .catch(() => {});
        }
    };

    container.addEventListener('touchstart', onTouchStart, { passive: true });
    container.addEventListener('touchmove', onTouchMove, { passive: false });
    container.addEventListener('touchend', onTouchEnd);
    container.addEventListener('touchcancel', onTouchEnd);

    sortableTouchHandlers.set(containerId, { onTouchStart, onTouchMove, onTouchEnd });
}

export function unregisterSortableTouch(containerId) {
    const handlers = sortableTouchHandlers.get(containerId);
    if (!handlers) return;
    const container = document.getElementById(containerId);
    if (container) {
        container.removeEventListener('touchstart', handlers.onTouchStart);
        container.removeEventListener('touchmove', handlers.onTouchMove);
        container.removeEventListener('touchend', handlers.onTouchEnd);
        container.removeEventListener('touchcancel', handlers.onTouchEnd);
    }
    sortableTouchHandlers.delete(containerId);
}

/* ===== AI primitives ===== */

const aiListObservers = new Map();
const aiScrollButtonObservers = new Map();

export const ai = {
    /* ---------- PromptInput auto-size ---------- */
    autosize(elementId, maxPx) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const max = Math.max(0, maxPx | 0);
        el.style.height = 'auto';
        const next = max > 0 ? Math.min(el.scrollHeight, max) : el.scrollHeight;
        el.style.height = next + 'px';
        el.style.overflowY = (max > 0 && el.scrollHeight > max) ? 'auto' : 'hidden';
    },

    /* ---------- AgentMessageList auto-scroll ---------- */
    observeAutoScroll(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // Tear down any previous registration on the same id — both the
        // MutationObserver and the scroll listener.
        const prev = aiListObservers.get(elementId);
        if (prev) {
            prev.observer.disconnect();
            if (prev.scrollTarget && prev.onScroll) {
                prev.scrollTarget.removeEventListener('scroll', prev.onScroll);
            }
        }

        const isNearBottom = () => (el.scrollHeight - el.scrollTop - el.clientHeight) < 96;
        let stick = true;

        // Named handler so disposeAutoScroll can pass the same reference to
        // removeEventListener — anonymous handlers can't be removed and
        // would otherwise leak per element across component remounts.
        const onScroll = () => { stick = isNearBottom(); };
        el.addEventListener('scroll', onScroll, { passive: true });

        const scrollToBottom = () => {
            el.scrollTop = el.scrollHeight;
        };

        // Initial pin to bottom
        scrollToBottom();

        const observer = new MutationObserver(() => {
            if (stick) scrollToBottom();
        });
        observer.observe(el, { childList: true, subtree: true, characterData: true });
        aiListObservers.set(elementId, { observer, scrollTarget: el, onScroll });
    },

    disposeAutoScroll(elementId) {
        const entry = aiListObservers.get(elementId);
        if (!entry) return;
        entry.observer.disconnect();
        if (entry.scrollTarget && entry.onScroll) {
            entry.scrollTarget.removeEventListener('scroll', entry.onScroll);
        }
        aiListObservers.delete(elementId);
    },

    /* ---------- Scroll helper used by StreamingText / message list ---------- */
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.scrollTop = el.scrollHeight;
    },

    /* ---------- ConversationScrollButton visibility ----------
       Reports (via .NET) whether the list is scrolled AWAY from the bottom, so a
       floating "scroll to latest" button can appear only when it's useful. Fires
       on scroll, on resize, and whenever content mutates (streaming grows the
       list), and only invokes .NET when the boolean actually flips — no per-event
       chatter. Mirrors observeAutoScroll's teardown discipline. */
    observeScrollButton(elementId, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const prev = aiScrollButtonObservers.get(elementId);
        if (prev) prev.dispose();

        // 8px slack so a list resting exactly at the bottom never shows the button.
        const isAway = () => (el.scrollHeight - el.scrollTop - el.clientHeight) > 8;
        let away = null;
        const evaluate = () => {
            const next = isAway();
            if (next === away) return;
            away = next;
            try { dotNetRef.invokeMethodAsync('OnScrollAwayChanged', next); } catch { /* circuit gone */ }
        };

        el.addEventListener('scroll', evaluate, { passive: true });
        const observer = new MutationObserver(evaluate);
        observer.observe(el, { childList: true, subtree: true, characterData: true });
        const ro = (typeof ResizeObserver !== 'undefined') ? new ResizeObserver(evaluate) : null;
        if (ro) ro.observe(el);

        evaluate();

        aiScrollButtonObservers.set(elementId, {
            dispose() {
                el.removeEventListener('scroll', evaluate);
                observer.disconnect();
                if (ro) ro.disconnect();
            }
        });
    },

    disposeScrollButton(elementId) {
        const entry = aiScrollButtonObservers.get(elementId);
        if (!entry) return;
        entry.dispose();
        aiScrollButtonObservers.delete(elementId);
    }
};

/* =============================================================
 * Tabs — measurement helper for the sliding underline indicator.
 * Returns the x-offset (relative to its offsetParent) and the width
 * of the currently-active trigger so Blazor can set the CSS vars
 * `--lumeo-tabs-indicator-x` / `--lumeo-tabs-indicator-w`.
 * ============================================================= */
export const tabs = {
    measure(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return { x: 0, width: 0 };
        return { x: el.offsetLeft, width: el.offsetWidth };
    }
};

/* =============================================================
 * Ripple — press-feedback helper for buttons and other tactile
 * surfaces. Attaches a pointerdown listener that spawns a scaling
 * circle at the cursor point. Driven by CSS keyframes + cleanup
 * via the animationend event. Honours `prefers-reduced-motion`
 * through CSS (the .lumeo-ripple-dot animation is disabled there).
 * ============================================================= */
function attachRipple(el) {
    if (!el || el.__lumeoRippleBound) return;
    el.__lumeoRippleBound = true;
    const handler = (e) => {
        const rect = el.getBoundingClientRect();
        const span = document.createElement('span');
        span.className = 'lumeo-ripple-dot';
        const size = Math.max(rect.width, rect.height);
        span.style.width = span.style.height = size + 'px';
        span.style.left = (e.clientX - rect.left - size / 2) + 'px';
        span.style.top = (e.clientY - rect.top - size / 2) + 'px';
        el.appendChild(span);
        span.addEventListener('animationend', () => span.remove(), { once: true });
    };
    el.__lumeoRippleHandler = handler;
    el.addEventListener('pointerdown', handler);
}

function detachRipple(el) {
    if (!el || !el.__lumeoRippleBound) return;
    el.removeEventListener('pointerdown', el.__lumeoRippleHandler);
    delete el.__lumeoRippleHandler;
    delete el.__lumeoRippleBound;
}

export const ripple = { attach: attachRipple, detach: detachRipple };

/* =============================================================
 * File input reset (#70) — clear a native <input type="file">'s
 * value so re-picking the SAME file re-fires `change`. Browsers
 * suppress `change` when the selected path equals the input's
 * current value; UploadTrigger has no accumulating list to mask
 * this, so it resets the element after every pick.
 * ============================================================= */
export function resetFileInput(el) {
    if (!el) return;
    try { el.value = ''; } catch { /* not a value-bearing input */ }
}

/* =============================================================
 * Reduced-motion gate (core) — mirror of the Lumeo.Motion helper
 * so core components (TouchRipple, …) can branch in C# before
 * spawning a JS/Blazor animation that a CSS `@media` block can't
 * fully neutralise. CSS-only animations stay gated in lumeo.css.
 * A function, not a cached bool, so an OS-setting toggle mid-
 * session is honoured next interaction. SSR/test-host safe.
 * ============================================================= */
export function prefersReducedMotion() {
    return typeof window !== 'undefined'
        && typeof window.matchMedia === 'function'
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/* Tooltip's focusin handler used to open on ANY DOM focus, including the focus a mouse
 * click leaves behind on a native <button> — browsers never clear focus after a click, so
 * the tooltip stayed open until focus moved elsewhere for an unrelated reason, long after
 * the mouse moved away (reported production bug). :focus-visible is the browser's own
 * signal for "this focus should be visually/behaviourally indicated" — true for keyboard
 * navigation, false for a mouse-click focus in supporting browsers — so checking it here
 * distinguishes "the user tabbed here" from "the user clicked here" without any manual
 * pointer-type bookkeeping. Falls back to true (old behaviour: always open on focus) if
 * :focus-visible or document.activeElement is unavailable, so this can only make MORE
 * focus-driven opens correct, never fewer, in an unsupported environment. */
export function isActiveElementFocusVisible() {
    var el = typeof document !== 'undefined' ? document.activeElement : null;
    if (!el || typeof el.matches !== 'function') return true;
    try { return el.matches(':focus-visible'); }
    catch { return true; }
}

/* TouchRipple — resolve the pointer's coordinates relative to the ripple
 * HOST element (the element the listener is bound to), not the event target.
 * The component previously used PointerEventArgs.OffsetX/OffsetY, which the
 * DOM defines relative to whatever child the pointer actually landed on — so
 * a ripple hosted around an icon/label spawned the circle at the child's
 * origin, visibly offset. Reading the host's getBoundingClientRect() and
 * subtracting from clientX/clientY fixes nested targets across browsers. */
export function touchRippleCoords(hostId, clientX, clientY) {
    const host = document.getElementById(hostId);
    if (!host) return { x: 0, y: 0 };
    const rect = host.getBoundingClientRect();
    return { x: clientX - rect.left, y: clientY - rect.top };
}

// Haptic feedback. No-op on browsers that don't expose Vibration API
// (e.g. iOS Safari) or when user has disabled motion. Safe to call without guards.
export function vibrate(ms) {
    try {
        if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
            navigator.vibrate(ms);
        }
    } catch { /* swallow — best-effort haptic */ }
}

// --- Viewport / responsive listener (2.1.3) ---
//
// Backs ResponsiveService. A single resize listener per circuit pings the
// .NET side with debounced viewport dimensions so Blazor components can
// react to breakpoint changes without each one registering its own listener.
//
// Debounce is 100ms — long enough to coalesce continuous drags of the
// browser-corner resize, short enough to feel reactive on orientation
// changes. The initial size is returned synchronously from
// registerViewportListener so consumers don't have to wait for the first
// resize before reading Width/Height.

let viewportDotnetRef = null;
let viewportResizeHandler = null;
let viewportDebounceTimer = 0;

export function registerViewportListener(dotnetRef) {
    // Idempotent: a second call replaces the dotnet ref (e.g. circuit restart)
    // and re-attaches the listener. Without this guard a circuit reconnect
    // would leave the old ref orphaned and the listener wired to a disposed
    // .NET handle.
    if (viewportResizeHandler) {
        window.removeEventListener('resize', viewportResizeHandler);
        viewportResizeHandler = null;
    }
    viewportDotnetRef = dotnetRef;

    viewportResizeHandler = () => {
        clearTimeout(viewportDebounceTimer);
        viewportDebounceTimer = setTimeout(() => {
            if (!viewportDotnetRef) return;
            try {
                viewportDotnetRef.invokeMethodAsync(
                    'OnViewportChange',
                    window.innerWidth,
                    window.innerHeight
                );
            } catch { /* circuit may have been torn down — swallow */ }
        }, 100);
    };

    window.addEventListener('resize', viewportResizeHandler, { passive: true });
    // Return the initial snapshot synchronously so the service can populate
    // Width/Height without a round-trip event.
    return { width: window.innerWidth, height: window.innerHeight };
}

export function unregisterViewportListener() {
    if (viewportResizeHandler) {
        window.removeEventListener('resize', viewportResizeHandler);
        viewportResizeHandler = null;
    }
    clearTimeout(viewportDebounceTimer);
    viewportDotnetRef = null;
}

// --- HTMLMediaElement (AudioPlayer / future VideoPlayer) ---
//
// Minimal pass-through helpers that invoke play()/pause() on an
// HTMLMediaElement reference. Lives here rather than inline in the
// component so the .NET side never touches IJSRuntime directly
// (project rule: all JS interop goes through ComponentInteropService).
// Both calls swallow errors — play() rejects on autoplay policy
// violations and that's expected behaviour, not a bug.

export function playMedia(el) {
    if (!el) return;
    try {
        const p = el.play();
        if (p && typeof p.catch === 'function') {
            p.catch(() => { /* autoplay blocked — caller already reflects paused state */ });
        }
    } catch { /* element detached / unsupported — swallow */ }
}

export function pauseMedia(el) {
    if (!el) return;
    try { el.pause(); } catch { /* swallow */ }
}

export function setMediaVolume(el, volume, muted) {
    if (!el) return;
    try {
        if (typeof volume === 'number') el.volume = Math.max(0, Math.min(1, volume));
        if (typeof muted === 'boolean') el.muted = muted;
    } catch { /* swallow */ }
}

export function seekMedia(el, seconds) {
    if (!el) return;
    try { el.currentTime = Math.max(0, seconds); } catch { /* swallow */ }
}

export function setPlaybackRate(el, rate) {
    if (!el) return;
    // Clamp to the range browsers actually honor; values outside ~0.25–4 are
    // ignored or throw on some engines.
    try { el.playbackRate = Math.max(0.25, Math.min(4, rate)); } catch { /* swallow */ }
}

// Reads the live `duration` and `currentTime` off a media element. Blazor
// event args for media events don't carry these — they're properties of the
// element. Returned as a fixed-shape object so the .NET side can use a
// matching record without extra JSON inspection. NaN/Infinity are coerced to
// 0 because the audio element exposes Infinity for live streams and NaN
// before metadata is loaded.
export function getMediaState(el) {
    if (!el) return { duration: 0, currentTime: 0 };
    const d = el.duration;
    const t = el.currentTime;
    return {
        duration: (Number.isFinite(d) && d > 0) ? d : 0,
        currentTime: Number.isFinite(t) ? t : 0
    };
}
