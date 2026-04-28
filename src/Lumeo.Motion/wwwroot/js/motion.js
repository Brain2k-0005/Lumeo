/* ===================================================== *
 * Lumeo.Motion — motion primitives JS module           *
 * ----------------------------------------------------- *
 * NumberTicker, TextReveal, BlurFade helpers.           *
 * All are RAF / IntersectionObserver driven and         *
 * deregister cleanly via dispose helpers.               *
 *                                                       *
 * Loaded lazily as _content/Lumeo.Motion/js/motion.js  *
 * by ComponentInteropService.GetMotionModuleAsync().    *
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

    /* ---------- BlurFade ----------
     * Paired with the CSS that now defaults to visible. If the element is already
     * in view when the observer attaches, leave it alone (prerender-friendly). If
     * it's out of view, explicitly hide it with data-motion-visible="false" so the
     * scroll-in animation still plays when it enters. */
    blurFade(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const delayMs = (options && options.delayMs) || 0;
        const once = !options || options.once !== false;
        const forceHidden = !!(options && options.forceHidden);

        // Check viewport position synchronously. Out-of-view → hide first so the
        // fade-in animation has somewhere to animate from when the observer fires.
        // forceHidden=true overrides viewport check — always hide first so docs demos
        // and Replay buttons reliably animate even when above the fold.
        const rect = el.getBoundingClientRect();
        const viewportH = window.innerHeight || document.documentElement.clientHeight;
        const isAboveFold = rect.top < viewportH && rect.bottom > 0;
        if (forceHidden || !isAboveFold) {
            el.setAttribute('data-motion-visible', 'false');
        }

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
