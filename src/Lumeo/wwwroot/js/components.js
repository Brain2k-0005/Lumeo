const clickOutsideHandlers = new Map();

document.addEventListener('mousedown', (e) => {
    for (const [id, { triggerElementId, dotnetRef }] of clickOutsideHandlers) {
        const el = document.getElementById(id);
        const trigger = triggerElementId ? document.getElementById(triggerElementId) : null;
        if (el && !el.contains(e.target) && (!trigger || !trigger.contains(e.target))) {
            dotnetRef.invokeMethodAsync('OnClickOutside', id);
        }
    }
});

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

export function lockScroll() {
    scrollLockCount++;
    if (scrollLockCount === 1) {
        document.body.style.overflow = 'hidden';
    }
}

export function unlockScroll() {
    scrollLockCount = Math.max(0, scrollLockCount - 1);
    if (scrollLockCount === 0) {
        document.body.style.overflow = '';
    }
}

const focusTrapHandlers = new Map();

export function setupFocusTrap(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return;

    const handler = (e) => {
        if (e.key !== 'Tab') return;
        const focusable = el.querySelectorAll(
            'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]):not([disabled])'
        );
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

    focusTrapHandlers.set(elementId, handler);
    el.addEventListener('keydown', handler);

    const focusable = el.querySelectorAll(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]):not([disabled])'
    );
    if (focusable.length > 0) {
        focusable[0].focus();
    }
}

export function removeFocusTrap(elementId) {
    const handler = focusTrapHandlers.get(elementId);
    if (handler) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('keydown', handler);
        }
        focusTrapHandlers.delete(elementId);
    }
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

        // Viewport bounds check (synchronous — no rAF to avoid stale refs)
        const cr = content.getBoundingClientRect();
        // Flip vertical if overflows bottom
        if (resolvedSide === 'bottom' && cr.bottom > window.innerHeight) {
            const newRefRect = reference.getBoundingClientRect();
            content.style.top = `${newRefRect.top - cr.height - gap}px`;
            content.style.transform = transform.replace('translateY(-100%)', '').trim() || '';
        }
        // Flip vertical if overflows top
        if (resolvedSide === 'top' && cr.top < 0) {
            const newRefRect = reference.getBoundingClientRect();
            content.style.top = `${newRefRect.bottom + gap}px`;
            content.style.transform = transform.replace('translateY(-100%)', '').replace('translateX(-50%) translateY(-100%)', 'translateX(-50%)').trim() || '';
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
        window.removeEventListener('scroll', onScrollOrResize, { capture: true });
        window.removeEventListener('resize', onScrollOrResize);
        positionCleanups.delete(contentId);
    };
    positionCleanups.set(contentId, cleanup);
}

export function unpositionFixed(contentId) {
    if (positionCleanups.has(contentId)) {
        positionCleanups.get(contentId)();
    }
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

// --- Drawer Swipe ---

const drawerHandlers = new Map();

export function registerDrawerSwipe(elementId, direction, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    const isHorizontal = direction === 'left' || direction === 'right';
    let startPos = 0;
    let currentPos = 0;
    let isDragging = false;

    const onTouchStart = (e) => {
        startPos = isHorizontal ? e.touches[0].clientX : e.touches[0].clientY;
        currentPos = startPos;
        isDragging = true;
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging) return;
        currentPos = isHorizontal ? e.touches[0].clientX : e.touches[0].clientY;
        const delta = currentPos - startPos;

        let shouldTranslate = false;
        if (direction === 'down') shouldTranslate = delta > 0;
        else if (direction === 'up') shouldTranslate = delta < 0;
        else if (direction === 'right') shouldTranslate = delta > 0;
        else if (direction === 'left') shouldTranslate = delta < 0;

        if (shouldTranslate) {
            if (isHorizontal) {
                el.style.transform = `translateX(${delta}px)`;
            } else {
                el.style.transform = `translateY(${delta}px)`;
            }
        }
    };

    const onTouchEnd = () => {
        if (!isDragging) return;
        isDragging = false;
        el.style.transition = '';
        const delta = currentPos - startPos;
        const absDelta = Math.abs(delta);

        let shouldDismiss = false;
        if (direction === 'down') shouldDismiss = delta > 100;
        else if (direction === 'up') shouldDismiss = delta < -100;
        else if (direction === 'right') shouldDismiss = delta > 100;
        else if (direction === 'left') shouldDismiss = delta < -100;

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

export function registerCarouselSwipe(elementId, orientation, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    let startX = 0, startY = 0;

    const onTouchStart = (e) => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
    };

    const onTouchEnd = (e) => {
        const endX = e.changedTouches[0].clientX;
        const endY = e.changedTouches[0].clientY;
        const deltaX = endX - startX;
        const deltaY = endY - startY;
        const threshold = 50;

        if (orientation === 'horizontal' && Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > threshold) {
            dotnetRef.invokeMethodAsync('OnSwipe', elementId, deltaX > 0 ? 'prev' : 'next');
        } else if (orientation === 'vertical' && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > threshold) {
            dotnetRef.invokeMethodAsync('OnSwipe', elementId, deltaY > 0 ? 'prev' : 'next');
        }
    };

    const onScroll = () => {
        const scrollPos = orientation === 'horizontal' ? el.scrollLeft : el.scrollTop;
        const maxScroll = orientation === 'horizontal'
            ? el.scrollWidth - el.clientWidth
            : el.scrollHeight - el.clientHeight;
        dotnetRef.invokeMethodAsync('OnScrollPosition', elementId, scrollPos, maxScroll);
    };

    el.addEventListener('touchstart', onTouchStart, { passive: true });
    el.addEventListener('touchend', onTouchEnd);
    el.addEventListener('scroll', onScroll, { passive: true });

    carouselHandlers.set(elementId, { onTouchStart, onTouchEnd, onScroll });
}

export function unregisterCarouselSwipe(elementId) {
    const handlers = carouselHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('touchstart', handlers.onTouchStart);
            el.removeEventListener('touchend', handlers.onTouchEnd);
            el.removeEventListener('scroll', handlers.onScroll);
        }
        carouselHandlers.delete(elementId);
    }
}

export function carouselScrollTo(elementId, index, behavior) {
    const el = document.getElementById(elementId);
    if (!el) return;
    const children = el.children;
    if (index >= 0 && index < children.length) {
        children[index].scrollIntoView({ behavior: behavior || 'smooth', block: 'nearest', inline: 'start' });
    }
}

// --- Resizable Handle ---

const resizeHandlers = new Map();

export function registerResizeHandle(elementId, direction, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    let isDragging = false;
    let startPos = 0;

    const onMouseDown = (e) => {
        isDragging = true;
        startPos = direction === 'horizontal' ? e.clientX : e.clientY;
        document.body.style.cursor = direction === 'horizontal' ? 'col-resize' : 'row-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    };

    const onMouseMove = (e) => {
        if (!isDragging) return;
        const currentPos = direction === 'horizontal' ? e.clientX : e.clientY;
        const delta = currentPos - startPos;
        startPos = currentPos;
        dotnetRef.invokeMethodAsync('OnResize', elementId, delta);
    };

    const onMouseUp = () => {
        if (!isDragging) return;
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotnetRef.invokeMethodAsync('OnResizeEnd', elementId);
    };

    el.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);

    resizeHandlers.set(elementId, { onMouseDown, onMouseMove, onMouseUp });
}

export function unregisterResizeHandle(elementId) {
    const handlers = resizeHandlers.get(elementId);
    if (handlers) {
        const el = document.getElementById(elementId);
        if (el) {
            el.removeEventListener('mousedown', handlers.onMouseDown);
        }
        document.removeEventListener('mousemove', handlers.onMouseMove);
        document.removeEventListener('mouseup', handlers.onMouseUp);
        resizeHandlers.delete(elementId);
    }
}

// --- Keyboard Shortcuts ---

let shortcutDotnetRef = null;
const shortcuts = new Map();

export function registerKeyboardShortcuts(dotnetRef) {
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

export function setupAutoResize(elementId, maxRows) {
    const el = document.getElementById(elementId);
    if (!el) return;
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
    resize(); // initial
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
                const digits = text.replace(/\D/g, '').slice(0, length);
                dotnetRef.invokeMethodAsync('OnOtpPaste', baseId, digits);
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

export function registerColumnResize(handleId, dotnetRef) {
    const handle = document.getElementById(handleId);
    if (!handle) return;
    let startX = 0;
    let isDragging = false;

    const onMouseDown = (e) => {
        isDragging = true;
        startX = e.clientX;
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
        e.stopPropagation();
    };
    const onMouseMove = (e) => {
        if (!isDragging) return;
        const delta = e.clientX - startX;
        startX = e.clientX;
        dotnetRef.invokeMethodAsync('OnColumnResize', handleId, delta);
    };
    const onMouseUp = () => {
        if (!isDragging) return;
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotnetRef.invokeMethodAsync('OnColumnResizeEnd', handleId);
    };
    handle.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
    columnResizeHandlers.set(handleId, { onMouseDown, onMouseMove, onMouseUp });
}

export function unregisterColumnResize(handleId) {
    const h = columnResizeHandlers.get(handleId);
    if (h) {
        const el = document.getElementById(handleId);
        if (el) el.removeEventListener('mousedown', h.onMouseDown);
        document.removeEventListener('mousemove', h.onMouseMove);
        document.removeEventListener('mouseup', h.onMouseUp);
        columnResizeHandlers.delete(handleId);
    }
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
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
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

/* ===================================================== *
 * Motion primitives                                     *
 * ----------------------------------------------------- *
 * NumberTicker, TextReveal, BlurFade helpers.           *
 * All are RAF / IntersectionObserver driven and         *
 * deregister cleanly via dispose helpers.               *
 * ===================================================== */

const motionTickers = new Map();       // elementId -> rafId
const motionObservers = new Map();     // elementId -> IntersectionObserver

function formatNumber(value, decimals) {
    const fixed = value.toFixed(decimals);
    // Locale-aware thousands separators without forcing a locale — use browser default.
    const [whole, frac] = fixed.split('.');
    const withSep = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return frac !== undefined ? `${withSep}.${frac}` : withSep;
}

export const motion = {
    /* ---------- NumberTicker ---------- */
    tickNumber(elementId, from, to, durationMs, decimals) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // Cancel any in-flight animation on the same element.
        const prev = motionTickers.get(elementId);
        if (prev) cancelAnimationFrame(prev);

        const start = performance.now();
        const delta = to - from;
        const dur = Math.max(1, durationMs | 0);
        const dec = Math.max(0, decimals | 0);

        const step = (now) => {
            const t = Math.min(1, (now - start) / dur);
            // easeOutCubic — snappy, settles nicely
            const eased = 1 - Math.pow(1 - t, 3);
            const current = from + delta * eased;
            el.textContent = formatNumber(current, dec);
            if (t < 1) {
                const id = requestAnimationFrame(step);
                motionTickers.set(elementId, id);
            } else {
                el.textContent = formatNumber(to, dec);
                motionTickers.delete(elementId);
            }
        };
        const id = requestAnimationFrame(step);
        motionTickers.set(elementId, id);
    },

    disposeTicker(elementId) {
        const id = motionTickers.get(elementId);
        if (id) cancelAnimationFrame(id);
        motionTickers.delete(elementId);
    },

    /* ---------- TextReveal ---------- */
    revealText(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const stagger = (options && options.stagger) || 80;
        const threshold = (options && options.threshold) || 0.3;

        const words = el.querySelectorAll('[data-motion-word]');
        words.forEach((w, i) => {
            w.style.transitionDelay = `${i * stagger}ms`;
        });

        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    el.setAttribute('data-motion-revealed', 'true');
                    observer.disconnect();
                    motionObservers.delete(elementId);
                    break;
                }
            }
        }, { threshold });
        observer.observe(el);
        motionObservers.set(elementId, observer);
    },

    /* ---------- BlurFade ---------- */
    blurFade(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const delayMs = (options && options.delayMs) || 0;
        const once = !options || options.once !== false;

        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    setTimeout(() => {
                        el.setAttribute('data-motion-visible', 'true');
                    }, delayMs);
                    if (once) {
                        observer.disconnect();
                        motionObservers.delete(elementId);
                    }
                } else if (!once) {
                    el.setAttribute('data-motion-visible', 'false');
                }
            }
        }, { threshold: 0.15 });
        observer.observe(el);
        motionObservers.set(elementId, observer);
    },

    disposeObserver(elementId) {
        const obs = motionObservers.get(elementId);
        if (obs) {
            obs.disconnect();
            motionObservers.delete(elementId);
        }
    }
};

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

        // Dispose any previous observer on the same id
        const prev = aiListObservers.get(elementId);
        if (prev) prev.disconnect();

        const isNearBottom = () => (el.scrollHeight - el.scrollTop - el.clientHeight) < 96;
        let stick = true;

        el.addEventListener('scroll', () => { stick = isNearBottom(); }, { passive: true });

        const scrollToBottom = () => {
            el.scrollTop = el.scrollHeight;
        };

        // Initial pin to bottom
        scrollToBottom();

        const observer = new MutationObserver(() => {
            if (stick) scrollToBottom();
        });
        observer.observe(el, { childList: true, subtree: true, characterData: true });
        aiListObservers.set(elementId, observer);
    },

    disposeAutoScroll(elementId) {
        const obs = aiListObservers.get(elementId);
        if (obs) {
            obs.disconnect();
            aiListObservers.delete(elementId);
        }
    },

    /* ---------- Scroll helper used by StreamingText / message list ---------- */
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.scrollTop = el.scrollHeight;
    }
};
