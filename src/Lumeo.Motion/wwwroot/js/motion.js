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
    },

    /* ---------- AnimatedBeam ----------
     * Calculates a cubic bezier path between two referenced DOM nodes
     * and animates a gradient offset along it. */
    animatedBeam(svgId, fromId, toId, options) {
        const svg = document.getElementById(svgId);
        if (!svg) return;

        const durationMs = (options && options.durationMs) || 3000;
        const delayMs = (options && options.delayMs) || 0;
        const curvature = (options && options.curvature) !== undefined ? options.curvature : 0.3;

        const pathEl = document.getElementById(svgId + '-path');
        const beamEl = document.getElementById(svgId + '-beam');
        if (!pathEl || !beamEl) return;

        const calcPath = () => {
            const fromEl = document.getElementById(fromId);
            const toEl = document.getElementById(toId);
            if (!fromEl || !toEl) return null;

            const svgRect = svg.getBoundingClientRect();
            const fr = fromEl.getBoundingClientRect();
            const tr = toEl.getBoundingClientRect();

            const x1 = fr.left + fr.width / 2 - svgRect.left;
            const y1 = fr.top + fr.height / 2 - svgRect.top;
            const x2 = tr.left + tr.width / 2 - svgRect.left;
            const y2 = tr.top + tr.height / 2 - svgRect.top;

            const dx = Math.abs(x2 - x1) * curvature;
            const cp1x = x1 + dx;
            const cp2x = x2 - dx;

            return `M ${x1} ${y1} C ${cp1x} ${y1}, ${cp2x} ${y2}, ${x2} ${y2}`;
        };

        const update = () => {
            const d = calcPath();
            if (d) {
                pathEl.setAttribute('d', d);
                beamEl.setAttribute('d', d);

                // Animate stroke-dashoffset for beam traversal
                const len = beamEl.getTotalLength ? beamEl.getTotalLength() : 200;
                beamEl.style.strokeDasharray = `${len * 0.2} ${len}`;
                beamEl.style.animation = 'none';
                void beamEl.offsetWidth; // reflow
                beamEl.style.animation = '';

                // Drive animation via CSS custom property
                beamEl.style.animationDuration = `${durationMs}ms`;
                beamEl.style.animationDelay = `${delayMs}ms`;
                beamEl.style.animationName = 'lumeo-beam-travel';
                beamEl.style.animationTimingFunction = 'linear';
                beamEl.style.animationIterationCount = 'infinite';
                beamEl.style.strokeDashoffset = `${len}`;
            }
        };

        // Inject the @keyframes if not already present
        if (!document.getElementById('lumeo-beam-kf')) {
            const style = document.createElement('style');
            style.id = 'lumeo-beam-kf';
            style.textContent = `
                @keyframes lumeo-beam-travel {
                    from { stroke-dashoffset: var(--lumeo-beam-len, 1000); }
                    to   { stroke-dashoffset: calc(var(--lumeo-beam-len, 1000) * -1.2); }
                }
            `;
            document.head.appendChild(style);
        }

        update();

        // Re-measure on resize
        const ro = new ResizeObserver(update);
        const fromEl2 = document.getElementById(fromId);
        const toEl2 = document.getElementById(toId);
        if (fromEl2) ro.observe(fromEl2);
        if (toEl2) ro.observe(toEl2);
        ro.observe(svg);
        motionObservers.set(svgId + '-beam-ro', { disconnect: () => ro.disconnect() });
    },

    disposeAnimatedBeam(svgId) {
        const ro = motionObservers.get(svgId + '-beam-ro');
        if (ro) { ro.disconnect(); motionObservers.delete(svgId + '-beam-ro'); }
    },

    /* ---------- Globe ----------
     * Pure canvas rotating dotted-sphere with a sweep glow.
     * Resolves CSS variable colors at runtime. */
    globe(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const canvas = document.getElementById(elementId + '-canvas');
        if (!canvas) return;

        const size = (options && options.size) || 300;
        const speed = (options && options.speed) || 1;
        const dotColorRaw = (options && options.dotColor) || 'var(--color-foreground)';
        const glowColorRaw = (options && options.glowColor) || 'var(--color-primary)';

        // Resolve CSS variables
        const resolveColor = (val) => {
            if (!val.startsWith('var(')) return val;
            const prop = val.slice(4, -1).trim();
            return getComputedStyle(document.documentElement).getPropertyValue(prop).trim() || '#888';
        };

        canvas.width = size * devicePixelRatio;
        canvas.height = size * devicePixelRatio;
        canvas.style.width = size + 'px';
        canvas.style.height = size + 'px';
        const ctx = canvas.getContext('2d');
        ctx.scale(devicePixelRatio, devicePixelRatio);

        const cx = size / 2, cy = size / 2, radius = size * 0.44;
        let angle = 0;
        let rafId;

        const draw = () => {
            ctx.clearRect(0, 0, size, size);
            const dotColor = resolveColor(dotColorRaw);
            const glowColor = resolveColor(glowColorRaw);
            const dotOpacity = dotColor;

            // Draw dots on sphere surface
            const latSteps = 14, lonSteps = 24;
            for (let lat = -90; lat <= 90; lat += 180 / latSteps) {
                for (let lon = -180; lon <= 180; lon += 360 / lonSteps) {
                    const phi = (lat * Math.PI) / 180;
                    const theta = (lon * Math.PI) / 180 + angle;
                    const x3 = radius * Math.cos(phi) * Math.cos(theta);
                    const y3 = radius * Math.sin(phi);
                    const z3 = radius * Math.cos(phi) * Math.sin(theta);
                    // Project: only front-facing dots (z3 >= -radius*0.1)
                    if (z3 < -radius * 0.05) continue;
                    const px = cx + x3;
                    const py = cy - y3;
                    const depth = (z3 + radius) / (2 * radius);
                    ctx.beginPath();
                    ctx.arc(px, py, 1.2, 0, Math.PI * 2);
                    ctx.fillStyle = dotColor;
                    ctx.globalAlpha = 0.2 + depth * 0.5;
                    ctx.fill();
                }
            }

            // Sweep glow arc
            ctx.globalAlpha = 0.18;
            const sweepStart = angle % (Math.PI * 2);
            const grad = ctx.createConicalGradient
                ? null // not widely available
                : null;
            ctx.beginPath();
            ctx.arc(cx, cy, radius, sweepStart, sweepStart + 0.9);
            ctx.lineWidth = 4;
            ctx.strokeStyle = glowColor;
            ctx.shadowColor = glowColor;
            ctx.shadowBlur = 10;
            ctx.stroke();
            ctx.shadowBlur = 0;
            ctx.globalAlpha = 1;

            angle += 0.005 * speed;
            rafId = requestAnimationFrame(draw);
        };

        draw();
        motionTickers.set(elementId + '-globe', rafId);

        // Store cleanup reference
        motionObservers.set(elementId + '-globe', { disconnect: () => {
            cancelAnimationFrame(rafId);
        }});
    },

    disposeGlobe(elementId) {
        const ref = motionObservers.get(elementId + '-globe');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-globe'); }
        const id = motionTickers.get(elementId + '-globe');
        if (id) { cancelAnimationFrame(id); motionTickers.delete(elementId + '-globe'); }
    },

    /* ---------- Dock ----------
     * Magnifies dock children based on cursor proximity. */
    dock(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const maxScale = (options && options.maxScale) || 1.8;
        const magnifyRadius = (options && options.magnifyRadius) || 100;

        const onMouseMove = (e) => {
            const items = el.children;
            for (const item of items) {
                const rect = item.getBoundingClientRect();
                const itemCx = rect.left + rect.width / 2;
                const itemCy = rect.top + rect.height / 2;
                const dist = Math.hypot(e.clientX - itemCx, e.clientY - itemCy);
                const t = Math.max(0, 1 - dist / magnifyRadius);
                const scale = 1 + (maxScale - 1) * t;
                item.style.transform = `scale(${scale.toFixed(3)})`;
            }
        };

        const onMouseLeave = () => {
            for (const item of el.children) {
                item.style.transform = '';
            }
        };

        el.addEventListener('mousemove', onMouseMove);
        el.addEventListener('mouseleave', onMouseLeave);

        motionObservers.set(elementId + '-dock', { disconnect: () => {
            el.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('mouseleave', onMouseLeave);
        }});
    },

    disposeDock(elementId) {
        const ref = motionObservers.get(elementId + '-dock');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-dock'); }
    },

    /* ---------- Spotlight ----------
     * Updates --lumeo-spotlight-x/y CSS vars on mousemove. */
    spotlight(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const onMouseMove = (e) => {
            const rect = el.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            el.style.setProperty('--lumeo-spotlight-x', `${x}px`);
            el.style.setProperty('--lumeo-spotlight-y', `${y}px`);
        };

        const onMouseLeave = () => {
            el.style.setProperty('--lumeo-spotlight-x', '-9999px');
            el.style.setProperty('--lumeo-spotlight-y', '-9999px');
        };

        el.addEventListener('mousemove', onMouseMove);
        el.addEventListener('mouseleave', onMouseLeave);

        motionObservers.set(elementId + '-spotlight', { disconnect: () => {
            el.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('mouseleave', onMouseLeave);
        }});
    },

    disposeSpotlight(elementId) {
        const ref = motionObservers.get(elementId + '-spotlight');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-spotlight'); }
    },

    /* ---------- Confetti ----------
     * Custom canvas-based confetti burst. No third-party deps. */
    confettiInit(elementId) {
        // Nothing to do at init for confetti — canvas is ready in DOM.
        // We just record that it's registered.
        motionObservers.set(elementId + '-confetti', { disconnect: () => {} });
    },

    confettiFire(elementId, options) {
        const canvas = document.getElementById(elementId + '-canvas');
        if (!canvas) return;

        const particleCount = (options && options.particleCount) || 80;
        const spread = (options && options.spread) || 70;
        const colors = (options && options.colors) || ['#ff595e','#ffca3a','#6a4c93','#1982c4','#8ac926'];
        const originX = (options && options.origin && options.origin.x) !== undefined ? options.origin.x : 0.5;
        const originY = (options && options.origin && options.origin.y) !== undefined ? options.origin.y : 0.5;

        const parent = canvas.parentElement;
        canvas.width = parent.offsetWidth || 300;
        canvas.height = parent.offsetHeight || 200;

        const ctx = canvas.getContext('2d');
        const particles = [];

        const ox = canvas.width * originX;
        const oy = canvas.height * originY;
        const spreadRad = (spread * Math.PI) / 180;

        for (let i = 0; i < particleCount; i++) {
            const angle = -Math.PI / 2 + (Math.random() - 0.5) * spreadRad * 2;
            const speed = 4 + Math.random() * 6;
            particles.push({
                x: ox, y: oy,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                color: colors[Math.floor(Math.random() * colors.length)],
                alpha: 1,
                size: 5 + Math.random() * 5,
                rotation: Math.random() * Math.PI * 2,
                rotationSpeed: (Math.random() - 0.5) * 0.3
            });
        }

        let rafId;
        const animate = () => {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            let alive = false;
            for (const p of particles) {
                if (p.alpha <= 0) continue;
                alive = true;
                p.x += p.vx;
                p.y += p.vy;
                p.vy += 0.15; // gravity
                p.vx *= 0.99;
                p.alpha -= 0.012;
                p.rotation += p.rotationSpeed;
                ctx.save();
                ctx.globalAlpha = Math.max(0, p.alpha);
                ctx.translate(p.x, p.y);
                ctx.rotate(p.rotation);
                ctx.fillStyle = p.color;
                ctx.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
                ctx.restore();
            }
            if (alive) {
                rafId = requestAnimationFrame(animate);
            } else {
                ctx.clearRect(0, 0, canvas.width, canvas.height);
            }
        };
        rafId = requestAnimationFrame(animate);
    },

    disposeConfetti(elementId) {
        const ref = motionObservers.get(elementId + '-confetti');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-confetti'); }
    },

    /* ---------- MagneticButton ----------
     * Translates the element toward the cursor within a radius. */
    magneticButton(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const radius = (options && options.radius) || 80;
        const maxTranslate = (options && options.maxTranslate) || 20;

        const onMouseMove = (e) => {
            const rect = el.getBoundingClientRect();
            const cx = rect.left + rect.width / 2;
            const cy = rect.top + rect.height / 2;
            const dx = e.clientX - cx;
            const dy = e.clientY - cy;
            const dist = Math.hypot(dx, dy);
            if (dist < radius) {
                const t = (1 - dist / radius);
                const tx = dx * t * (maxTranslate / radius) * 2;
                const ty = dy * t * (maxTranslate / radius) * 2;
                el.style.transform = `translate(${tx.toFixed(2)}px, ${ty.toFixed(2)}px)`;
            } else {
                el.style.transform = '';
            }
        };

        const onMouseLeave = () => {
            el.style.transform = '';
        };

        window.addEventListener('mousemove', onMouseMove);
        el.addEventListener('mouseleave', onMouseLeave);

        motionObservers.set(elementId + '-magnetic', { disconnect: () => {
            window.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('mouseleave', onMouseLeave);
        }});
    },

    disposeMagneticButton(elementId) {
        const ref = motionObservers.get(elementId + '-magnetic');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-magnetic'); }
    },

    /* ---------- MagicCard ----------
     * Follows cursor with a spotlight radial gradient + subtle 3D tilt. */
    magicCard(elementId, options) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const maxTilt = (options && options.maxTilt) || 8;

        const onMouseMove = (e) => {
            const rect = el.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            const cx = rect.width / 2;
            const cy = rect.height / 2;
            const rotateX = ((y - cy) / cy) * -maxTilt;
            const rotateY = ((x - cx) / cx) * maxTilt;
            el.style.setProperty('--lumeo-magic-x', `${x}px`);
            el.style.setProperty('--lumeo-magic-y', `${y}px`);
            el.style.transform = `perspective(600px) rotateX(${rotateX.toFixed(2)}deg) rotateY(${rotateY.toFixed(2)}deg)`;
        };

        const onMouseLeave = () => {
            el.style.setProperty('--lumeo-magic-x', '-9999px');
            el.style.setProperty('--lumeo-magic-y', '-9999px');
            el.style.transform = '';
        };

        el.addEventListener('mousemove', onMouseMove);
        el.addEventListener('mouseleave', onMouseLeave);

        motionObservers.set(elementId + '-magic', { disconnect: () => {
            el.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('mouseleave', onMouseLeave);
        }});
    },

    disposeMagicCard(elementId) {
        const ref = motionObservers.get(elementId + '-magic');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-magic'); }
    },

    /* ---------- HoverBorderGradient ----------
     * Rotates a conic gradient border following cursor angle around the element. */
    hoverBorderGradient(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const onMouseMove = (e) => {
            const rect = el.getBoundingClientRect();
            const cx = rect.left + rect.width / 2;
            const cy = rect.top + rect.height / 2;
            const angle = Math.atan2(e.clientY - cy, e.clientX - cx) * (180 / Math.PI) + 90;
            el.style.setProperty('--lumeo-hbg-angle', `${angle.toFixed(1)}deg`);
        };

        const onMouseLeave = () => {
            el.style.setProperty('--lumeo-hbg-angle', '0deg');
        };

        el.addEventListener('mousemove', onMouseMove);
        el.addEventListener('mouseleave', onMouseLeave);

        motionObservers.set(elementId + '-hbg', { disconnect: () => {
            el.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('mouseleave', onMouseLeave);
        }});
    },

    disposeHoverBorderGradient(elementId) {
        const ref = motionObservers.get(elementId + '-hbg');
        if (ref) { ref.disconnect(); motionObservers.delete(elementId + '-hbg'); }
    }
};
