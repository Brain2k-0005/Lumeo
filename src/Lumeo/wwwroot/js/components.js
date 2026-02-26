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
