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

export function positionFixed(contentId, referenceId, align, matchWidth) {
    const content = document.getElementById(contentId);
    const reference = document.getElementById(referenceId);
    if (!content || !reference) return;

    const refRect = reference.getBoundingClientRect();
    const gap = 4;

    content.style.position = 'fixed';
    content.style.zIndex = '50';
    content.style.top = `${refRect.bottom + gap}px`;

    if (matchWidth) {
        content.style.width = `${refRect.width}px`;
    }

    switch (align) {
        case 'center':
            content.style.left = `${refRect.left + refRect.width / 2}px`;
            content.style.transform = 'translateX(-50%)';
            break;
        case 'end':
            content.style.left = 'auto';
            content.style.right = `${window.innerWidth - refRect.right}px`;
            break;
        default:
            content.style.left = `${refRect.left}px`;
            break;
    }

    // Viewport bounds check
    requestAnimationFrame(() => {
        if (!content.isConnected) return;
        const cr = content.getBoundingClientRect();
        if (cr.bottom > window.innerHeight) {
            content.style.top = `${refRect.top - cr.height - gap}px`;
        }
        if (cr.right > window.innerWidth) {
            content.style.left = `${window.innerWidth - cr.width - 8}px`;
            content.style.transform = '';
            content.style.right = 'auto';
        }
        if (cr.left < 0) {
            content.style.left = '8px';
            content.style.transform = '';
            content.style.right = 'auto';
        }
    });
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

// --- Drawer Swipe ---

const drawerHandlers = new Map();

export function registerDrawerSwipe(elementId, dotnetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    let startY = 0;
    let currentY = 0;
    let isDragging = false;

    const onTouchStart = (e) => {
        startY = e.touches[0].clientY;
        currentY = startY;
        isDragging = true;
        el.style.transition = 'none';
    };

    const onTouchMove = (e) => {
        if (!isDragging) return;
        currentY = e.touches[0].clientY;
        const deltaY = currentY - startY;
        if (deltaY > 0) {
            el.style.transform = `translateY(${deltaY}px)`;
        }
    };

    const onTouchEnd = () => {
        if (!isDragging) return;
        isDragging = false;
        el.style.transition = '';
        const deltaY = currentY - startY;
        if (deltaY > 100) {
            dotnetRef.invokeMethodAsync('OnSwipeDismiss');
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
            dotnetRef.invokeMethodAsync('OnSwipe', deltaX > 0 ? 'prev' : 'next');
        } else if (orientation === 'vertical' && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > threshold) {
            dotnetRef.invokeMethodAsync('OnSwipe', deltaY > 0 ? 'prev' : 'next');
        }
    };

    const onScroll = () => {
        const scrollPos = orientation === 'horizontal' ? el.scrollLeft : el.scrollTop;
        const maxScroll = orientation === 'horizontal'
            ? el.scrollWidth - el.clientWidth
            : el.scrollHeight - el.clientHeight;
        dotnetRef.invokeMethodAsync('OnScrollPosition', scrollPos, maxScroll);
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
        dotnetRef.invokeMethodAsync('OnResize', delta);
    };

    const onMouseUp = () => {
        if (!isDragging) return;
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotnetRef.invokeMethodAsync('OnResizeEnd');
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
            for (const [id, { combo, preventDefault }] of shortcuts) {
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
