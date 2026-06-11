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
export function lockScroll() {
    scrollLockCount++;
    if (scrollLockCount === 1) {
        document.body.style.overflow = 'hidden';
        document.documentElement.style.overflow = 'hidden';
    }
}

export function unlockScroll() {
    scrollLockCount = Math.max(0, scrollLockCount - 1);
    if (scrollLockCount === 0) {
        document.body.style.overflow = '';
        document.documentElement.style.overflow = '';
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

    // Web Animations API: returns running + already-finished animations
    // (fill-mode keeps finished ones alive in this list).
    const slideAnimations = el.getAnimations({ subtree: false })
        .filter(a => typeof a.animationName === 'string'
                  && a.animationName.startsWith('slide-in-from-'));

    if (slideAnimations.length === 0) {
        // No slide animation found — defensive: clear both transform AND
        // animation (in case a partial state somehow remains).
        el.style.setProperty('transform', 'none', 'important');
        el.style.setProperty('animation', 'none', 'important');
        return;
    }

    await Promise.all(slideAnimations.map(async (anim) => {
        try { await anim.finished; }
        catch { /* playState 'cancelled' or 'idle' — ignore */ }
    }));

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

// --- Floating Position ---

const positionCleanups = new Map();

export function positionFixed(contentId, referenceId, align, matchWidth, side) {
    const content = document.getElementById(contentId);
    const reference = document.getElementById(referenceId);
    if (!content || !reference) return;

    const resolvedSide = side || 'bottom';
    const gap = 4;

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

        // Calculate preferred position
        let top, left, right;
        let transform = '';

        if (resolvedSide === 'top') {
            top = refRect.top - gap;
            switch (align) {
                case 'center':
                    left = refRect.left + refRect.width / 2;
                    transform = 'translateX(-50%) translateY(-100%)';
                    break;
                case 'end':
                    left = refRect.right;
                    transform = 'translateX(-100%) translateY(-100%)';
                    break;
                default:
                    left = refRect.left;
                    transform = 'translateY(-100%)';
                    break;
            }
        } else if (resolvedSide === 'left') {
            left = refRect.left - gap;
            transform = 'translateX(-100%)';
            switch (align) {
                case 'center':
                    top = refRect.top + refRect.height / 2;
                    transform = 'translateX(-100%) translateY(-50%)';
                    break;
                case 'end':
                    top = refRect.bottom;
                    transform = 'translateX(-100%) translateY(-100%)';
                    break;
                default:
                    top = refRect.top;
                    break;
            }
        } else if (resolvedSide === 'right') {
            left = refRect.right + gap;
            switch (align) {
                case 'center':
                    top = refRect.top + refRect.height / 2;
                    transform = 'translateY(-50%)';
                    break;
                case 'end':
                    top = refRect.bottom;
                    transform = 'translateY(-100%)';
                    break;
                default:
                    top = refRect.top;
                    break;
            }
        } else {
            // bottom (default)
            top = refRect.bottom + gap;
            switch (align) {
                case 'center':
                    left = refRect.left + refRect.width / 2;
                    transform = 'translateX(-50%)';
                    break;
                case 'end':
                    left = refRect.right;
                    transform = 'translateX(-100%)';
                    break;
                default:
                    left = refRect.left;
                    break;
            }
        }

        // Apply initial position
        content.style.top = `${top}px`;
        content.style.left = left != null ? `${left}px` : '';
        content.style.right = right != null ? `${right}px` : '';
        content.style.transform = transform;

        // Viewport bounds check (synchronous — no rAF to avoid stale refs).
        // Reset any prior maxHeight so we measure the natural content size
        // (otherwise a previous tight clamp would stick across reopens).
        content.style.maxHeight = '';
        content.style.overflow = '';

        // Force a synchronous layout BEFORE measuring. Without this, an
        // in-flight animation scale-up or a Blazor render that hasn't
        // committed yet can return stale / pre-final dimensions —
        // empirically observed: cr.height = 1574 when the popover's
        // settled height was 310. Reading offsetHeight forces the
        // browser to flush layout.
        void content.offsetHeight;
        const cr = content.getBoundingClientRect();

        // Flip vertical if overflows bottom — but ONLY if flipping up actually
        // gives us more usable space. Without this guard, a popover that's
        // taller than the trigger's `top` (e.g. a calendar grown to fill its
        // parent flex container — observed at ~1574px inside a Sheet) ends
        // up with `top: triggerTop - contentHeight - gap` going far negative,
        // rendering off-screen at the top. Clamp to viewport with maxHeight.
        if (resolvedSide === 'bottom' && cr.bottom > window.innerHeight) {
            const newRefRect = reference.getBoundingClientRect();
            const spaceAbove = newRefRect.top - 8;        // 8px breathing room
            const spaceBelow = window.innerHeight - newRefRect.bottom - 8;
            if (spaceAbove >= cr.height + gap) {
                // Flip up — fits naturally
                content.style.top = `${newRefRect.top - cr.height - gap}px`;
                content.style.transform = transform.replace('translateY(-100%)', '').trim() || '';
            } else if (spaceAbove > spaceBelow) {
                // More room above than below — flip up and cap height
                content.style.top = `8px`;
                content.style.maxHeight = `${spaceAbove - gap}px`;
                content.style.overflow = 'auto';
                content.style.transform = transform.replace('translateY(-100%)', '').trim() || '';
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
                content.style.transform = transform.replace('translateY(-100%)', '').replace('translateX(-50%) translateY(-100%)', 'translateX(-50%)').trim() || '';
            } else if (spaceBelow > spaceAbove) {
                content.style.top = `${newRefRect.bottom + gap}px`;
                content.style.maxHeight = `${spaceBelow}px`;
                content.style.overflow = 'auto';
                content.style.transform = transform.replace('translateY(-100%)', '').replace('translateX(-50%) translateY(-100%)', 'translateX(-50%)').trim() || '';
            } else {
                content.style.top = `8px`;
                content.style.maxHeight = `${spaceAbove - gap}px`;
                content.style.overflow = 'auto';
            }
        }
        // Clamp horizontal
        if (cr.right > window.innerWidth) {
            content.style.left = `${window.innerWidth - cr.width - 8}px`;
            content.style.transform = '';
        }
        if (cr.left < 0) {
            content.style.left = '8px';
            content.style.transform = '';
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

    const cleanup = () => {
        cancelAnimationFrame(rafId);
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

    let startX = 0, startY = 0;
    let currentPos = 0;
    let isDragging = false;
    let active = false;       // gesture passed activation threshold
    let aborted = false;      // axis-lock determined this gesture is for the wrong axis

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
        currentPos = isHorizontal ? startX : startY;
        isDragging = true;
        active = false;
        aborted = false;
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging || aborted) return;
        const x = e.touches[0].clientX;
        const y = e.touches[0].clientY;
        const dx = x - startX;
        const dy = y - startY;
        currentPos = isHorizontal ? x : y;

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
        // Dismiss threshold is measured on raw axis delta (intent), not the
        // rubber-banded visual translate, so the gesture feels predictable
        // regardless of how far the rubber-band let the sheet travel.
        const shouldDismiss = Math.sign(delta) === dismissSign && Math.abs(delta) > DISMISS_THRESHOLD;
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
            if (r.skipEditable && e.target instanceof Element &&
                e.target.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]')) continue;
            e.preventDefault();
            return;
        }
    };
    el.addEventListener('keydown', handler);
    preventDefaultKeyHandlers.set(elementId, handler);
}

export function unregisterPreventDefaultKeys(elementId) {
    const handler = preventDefaultKeyHandlers.get(elementId);
    if (handler) {
        const el = document.getElementById(elementId);
        if (el) el.removeEventListener('keydown', handler);
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
        dotnetRef.invokeMethodAsync('OnCalendarSwipe', elementId, deltaX < 0 ? 'next' : 'prev');
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
        dotnetRef.invokeMethodAsync('OnGallerySwipe', elementId, deltaX < 0 ? 'next' : 'prev');
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

        const direction = deltaX < 0 ? 'next' : 'prev';
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
            for (const [id, { combo, preventDefault }] of shortcuts) {
                // Skip modifier-less shortcuts when focus is inside an editable element
                if (isEditable) {
                    const hasModifier = combo.includes('ctrl') || combo.includes('alt') || combo.includes('meta');
                    if (!hasModifier) continue;
                }
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

export function addShortcut(id, combo, preventDefault) {
    shortcuts.set(id, { combo, preventDefault });
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

export function scrollspyScrollTo(containerId, sectionId, smooth) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const viewport = findScrollableViewport(container);
    const section = document.getElementById(sectionId);
    if (!section) return;

    viewport.scrollTo({
        top: section.offsetTop,
        behavior: smooth ? 'smooth' : 'auto'
    });
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
        items[index].focus();
        return index;
    }
    return -1;
}

export function getMenuItemCount(containerId) {
    return getMenuItems(containerId).length;
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

// --- DataGrid Column Resize ---

const columnResizeHandlers = new Map();

export function registerColumnResize(handleId, dotnetRef, minWidth, maxWidth) {
    const handle = document.getElementById(handleId);
    if (!handle) return;
    const th = handle.closest('th');
    if (!th) return;

    let startX = 0;
    let startWidth = 0;
    let currentWidth = 0;
    let isDragging = false;
    let colBodyCells = [];
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
            if (row.children[colIndex]) cells.push(row.children[colIndex]);
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
        isDragging = true;
        activePointerId = e.pointerId;
        startX = e.clientX;
        startWidth = th.getBoundingClientRect().width;
        currentWidth = startWidth;
        pendingWidth = startWidth;
        colBodyCells = gatherBodyCells();
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
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
    };
    const onPointerMove = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        const delta = e.clientX - startX;
        let w = startWidth + delta;
        if (w < min) w = min;
        else if (w > max) w = max;
        if (w === pendingWidth) {
            if (e.cancelable) e.preventDefault();
            return;
        }
        pendingWidth = w;
        if (!rafId) rafId = requestAnimationFrame(flushPendingWidth);
        // Block scroll on touch devices while actively dragging.
        if (e.cancelable) e.preventDefault();
    };
    const onPointerUp = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        isDragging = false;
        if (rafId) { cancelAnimationFrame(rafId); rafId = 0; }
        // Apply any pending frame synchronously so the committed width
        // matches what the user released on (no half-frame visual snap).
        if (pendingWidth !== currentWidth) {
            currentWidth = pendingWidth;
            applyWidth(pendingWidth);
        }
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        try { handle.releasePointerCapture(e.pointerId); } catch (_) { }
        activePointerId = null;
        dotnetRef.invokeMethodAsync('OnColumnResizeCommit', handleId, currentWidth);
    };
    const onPointerCancel = (e) => {
        if (!isDragging || e.pointerId !== activePointerId) return;
        isDragging = false;
        if (rafId) { cancelAnimationFrame(rafId); rafId = 0; }
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        try { handle.releasePointerCapture(e.pointerId); } catch (_) { }
        activePointerId = null;
        // No commit on cancel — the user aborted (e.g. system gesture took over).
    };
    handle.addEventListener('pointerdown', onPointerDown);
    // passive: false so preventDefault works inside pointermove on touch.
    handle.addEventListener('pointermove', onPointerMove, { passive: false });
    handle.addEventListener('pointerup', onPointerUp);
    handle.addEventListener('pointercancel', onPointerCancel);
    columnResizeHandlers.set(handleId, { handle, onPointerDown, onPointerMove, onPointerUp, onPointerCancel });
}

export function unregisterColumnResize(handleId) {
    const h = columnResizeHandlers.get(handleId);
    if (h) {
        const el = h.handle || document.getElementById(handleId);
        if (el) {
            el.removeEventListener('pointerdown', h.onPointerDown);
            el.removeEventListener('pointermove', h.onPointerMove);
            el.removeEventListener('pointerup', h.onPointerUp);
            el.removeEventListener('pointercancel', h.onPointerCancel);
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
    const headers = grid.querySelectorAll('th[data-col-id]');
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
        snapshot[colId] = { left: th.getBoundingClientRect().left, cells };
    });
    columnReorderSnapshots.set(gridId, snapshot);
}

export function animateColumnReorder(gridId, durationMs) {
    const snapshot = columnReorderSnapshots.get(gridId);
    columnReorderSnapshots.delete(gridId);
    if (!snapshot) return;
    const grid = document.querySelector(`[data-grid-id="${CSS.escape(gridId)}"]`);
    if (!grid) return;
    const duration = Number(durationMs) > 0 ? Number(durationMs) : 200;

    const headers = grid.querySelectorAll('th[data-col-id]');
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

    const eventTarget = scrollTarget === window ? window : scrollTarget;
    eventTarget.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll, { passive: true });
    affixHandlers.set(elementId, { onScroll, placeholder, eventTarget });

    // Initial check
    requestAnimationFrame(onScroll);
}

export function unregisterAffix(elementId) {
    const handler = affixHandlers.get(elementId);
    if (handler) {
        handler.eventTarget.removeEventListener('scroll', handler.onScroll);
        window.removeEventListener('resize', handler.onScroll);
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

export function registerBackToTop(id, dotnetRef, threshold) {
    // Clean up previous registration for this id
    if (backToTopHandlers.has(id)) {
        const prev = backToTopHandlers.get(id);
        window.removeEventListener('scroll', prev.handler);
    }

    const effectiveThreshold = threshold || 300;
    const handler = () => {
        const scrollY = window.scrollY || document.documentElement.scrollTop;
        const visible = scrollY > effectiveThreshold;
        dotnetRef.invokeMethodAsync('OnScrollVisibilityChanged', id, visible);
    };

    window.addEventListener('scroll', handler, { passive: true });
    backToTopHandlers.set(id, { handler, dotnetRef });
    handler(); // initial check
}

export function unregisterBackToTop(id) {
    const entry = backToTopHandlers.get(id);
    if (entry) {
        window.removeEventListener('scroll', entry.handler);
        backToTopHandlers.delete(id);
    }
}

export function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// --- Mention: get textarea caret coordinates ---

export function getTextareaCaretPosition(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return { top: 0, left: 0, selectionStart: 0 };

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

    const top = elRect.top + span.offsetTop - el.scrollTop;
    const left = elRect.left + span.offsetLeft - el.scrollLeft;
    document.body.removeChild(div);

    return { top, left, selectionStart };
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
