// Project-level defaults written by `lumeo theme apply --preset <code>`.
//
// Previously this did a synchronous XHR to `/lumeo-theme.json` on every page
// load — which (a) triggered a deprecation warning in Chrome, (b) blocked the
// main thread, and (c) 404'd (and logged a console error) on every site that
// didn't ship the file.
//
// Now: the `lumeo` CLI inlines the JSON into index.html as a
// <script id="lumeo-theme-defaults" type="application/json">...</script> tag,
// read synchronously from the DOM. Sites without the tag get null — no fetch,
// no network, no 404.
function loadLumeoThemeDefaults() {
    try {
        const tag = document.getElementById('lumeo-theme-defaults');
        if (tag && tag.textContent) return JSON.parse(tag.textContent);
    } catch (_) { /* malformed JSON — fall back to built-ins */ }
    return null;
}

function lumeoDefault(defaults, key) {
    if (!defaults) return null;
    const v = defaults[key];
    return (v === undefined || v === null) ? null : String(v);
}

// Google Fonts map — keep in sync with LumeoPresetOptions.Fonts in src/Lumeo/Theming/.
const lumeoFontMap = {
    'inter':          { href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap',         family: "'Inter', system-ui, sans-serif" },
    'geist':          { href: 'https://fonts.googleapis.com/css2?family=Geist:wght@400;500;600;700&display=swap',         family: "'Geist', system-ui, sans-serif" },
    'ibm-plex-sans':  { href: 'https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&display=swap', family: "'IBM Plex Sans', system-ui, sans-serif" },
    'jetbrains-mono': { href: 'https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;700&display=swap',    family: "'JetBrains Mono', ui-monospace, monospace" },
    'fira-code':      { href: 'https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;700&display=swap',         family: "'Fira Code', ui-monospace, monospace" },
};

function applyLumeoFont(fontId, localPath) {
    // Remove any prior injection if the font is unset/system — so switching back
    // to system actually reverts (not just stacks another override).
    const existingLink = document.getElementById('lumeo-font-link');
    const existingStyle = document.getElementById('lumeo-font-override');
    if (!fontId || fontId === 'system') {
        if (existingLink) existingLink.remove();
        if (existingStyle) existingStyle.remove();
        return;
    }
    const entry = lumeoFontMap[fontId];
    if (!entry) return; // unknown font id — silent no-op
    // Prefer a self-hosted CSS if the CLI downloaded one — avoids the runtime
    // dependency on fonts.googleapis.com that the Google href would introduce.
    const href = localPath && typeof localPath === 'string' && localPath.length > 0
        ? localPath
        : entry.href;
    if (!existingLink || existingLink.getAttribute('href') !== href) {
        if (existingLink) existingLink.remove();
        const link = document.createElement('link');
        link.id = 'lumeo-font-link';
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }
    const css = `:root{font-family:${entry.family};}`;
    if (!existingStyle) {
        const style = document.createElement('style');
        style.id = 'lumeo-font-override';
        style.textContent = css;
        document.head.appendChild(style);
    } else if (existingStyle.textContent !== css) {
        existingStyle.textContent = css;
    }
}

window.themeManager = {
    init: function () {
        var defaults = loadLumeoThemeDefaults();

        // Ensure portal-rendered components (Dialog / Sheet / Drawer / Toast /
        // Popover / Tooltip / DataGrid fullscreen) inherit Lumeo's theme tokens.
        // These overlays sit at the document root, outside any app-level wrapper,
        // so the theme classes MUST live on <body> — otherwise they render with
        // the browser's default white/transparent background and look broken in
        // dark mode. We apply this at init so consumers don't have to remember
        // to add the classes to their index.html / _Host.cshtml manually.
        if (document.body) {
            if (!document.body.classList.contains('bg-background')) {
                document.body.classList.add('bg-background');
            }
            if (!document.body.classList.contains('text-foreground')) {
                document.body.classList.add('text-foreground');
            }
        } else {
            // theme.js may load before <body> is parsed (head <script>). Re-run
            // once DOM is ready so the classes still land.
            document.addEventListener('DOMContentLoaded', function () {
                document.body.classList.add('bg-background', 'text-foreground');
            }, { once: true });
        }

        // Dark mode — localStorage override wins; else JSON default; else OS preference.
        var mode = localStorage.getItem('theme-mode') || localStorage.getItem('theme');
        if (!mode) {
            var d = lumeoDefault(defaults, 'dark');
            if (d === 'true') mode = 'dark';
            else if (d === 'false') mode = 'light';
        }
        if (mode === 'dark' || (!mode && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        // Color scheme — localStorage wins; else JSON default.
        const scheme = localStorage.getItem('theme-scheme') || lumeoDefault(defaults, 'theme');
        if (scheme && scheme !== 'zinc' && scheme !== '') {
            document.documentElement.setAttribute('data-theme', scheme);
        }
        // Radius
        var radius = localStorage.getItem('theme-radius');
        if (radius === null) radius = lumeoDefault(defaults, 'radius');
        if (radius !== null) {
            document.documentElement.style.setProperty('--radius', radius + 'rem');
        }
        // Style
        const style = localStorage.getItem('theme-style') || lumeoDefault(defaults, 'style');
        if (style === 'new-york') {
            document.documentElement.classList.add('style-new-york');
        }
        // Base color
        const baseColor = localStorage.getItem('theme-base-color') || lumeoDefault(defaults, 'baseColor');
        if (baseColor && baseColor !== 'slate') {
            document.documentElement.setAttribute('data-base-color', baseColor);
        }
        // Menu accent
        const menuAccent = localStorage.getItem('theme-menu-accent') || lumeoDefault(defaults, 'menuAccent');
        if (menuAccent && menuAccent !== 'subtle') {
            document.documentElement.setAttribute('data-menu-accent', menuAccent);
        }
        // Menu color
        const menuColor = localStorage.getItem('theme-menu-color') || lumeoDefault(defaults, 'menuColor');
        if (menuColor === 'dark') {
            document.documentElement.style.setProperty('--color-sidebar', 'hsl(220 13% 10%)');
            document.documentElement.style.setProperty('--color-sidebar-foreground', 'hsl(0 0% 95%)');
        } else if (menuColor === 'light') {
            document.documentElement.style.setProperty('--color-sidebar', 'hsl(0 0% 100%)');
            document.documentElement.style.setProperty('--color-sidebar-foreground', 'hsl(220 13% 10%)');
        }
        // Direction (RTL / LTR). Applied early so first paint is correct and
        // browser-native logical properties flip with no visible reflow.
        const dir = localStorage.getItem('lumeo.direction');
        if (dir === 'rtl') {
            document.documentElement.setAttribute('dir', 'rtl');
        } else if (dir === 'ltr') {
            document.documentElement.setAttribute('dir', 'ltr');
        }
        // Font — localStorage (raw CSS block) wins; else JSON default key 'font' maps
        // to a Google Fonts <link> + font-family override auto-injected here, so
        // consumers who apply a preset with font=inter never need to edit index.html.
        const fontCss = localStorage.getItem('theme-font-css');
        if (fontCss) {
            var el = document.getElementById('lumeo-font-override');
            if (!el) {
                el = document.createElement('style');
                el.id = 'lumeo-font-override';
                document.head.appendChild(el);
            }
            el.textContent = fontCss;
        } else {
            applyLumeoFont(lumeoDefault(defaults, 'font'), lumeoDefault(defaults, 'fontLocalPath'));
        }
    },
    setMode: function (mode) {
        if (mode === 'system') {
            localStorage.removeItem('theme-mode');
            localStorage.removeItem('theme');
            if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
                document.documentElement.classList.add('dark');
            } else {
                document.documentElement.classList.remove('dark');
            }
        } else if (mode === 'dark') {
            localStorage.setItem('theme-mode', 'dark');
            localStorage.setItem('theme', 'dark');
            document.documentElement.classList.add('dark');
        } else {
            localStorage.setItem('theme-mode', 'light');
            localStorage.setItem('theme', 'light');
            document.documentElement.classList.remove('dark');
        }
        this._notifyThemeChanged();
    },
    _notifyThemeChanged: function () {
        // Fires after ANY theme-related change (mode, scheme, radius, style,
        // base color, menu color, menu accent, font). Consumers like the chart
        // module listen to this to re-read CSS variables and repaint.
        try {
            document.dispatchEvent(new CustomEvent('lumeo:theme-changed'));
        } catch (_) { /* older browsers — intentionally silent */ }
    },
    getMode: function () {
        var mode = localStorage.getItem('theme-mode') || localStorage.getItem('theme');
        if (!mode) return 'system';
        if (mode === 'dark') return 'dark';
        return 'light';
    },
    isDark: function () {
        return document.documentElement.classList.contains('dark');
    },
    toggle: function () {
        var isDark = document.documentElement.classList.toggle('dark');
        localStorage.setItem('theme-mode', isDark ? 'dark' : 'light');
        localStorage.setItem('theme', isDark ? 'dark' : 'light');
        this._notifyThemeChanged();
    },
    setScheme: function (scheme) {
        if (!scheme || scheme === 'zinc') {
            localStorage.removeItem('theme-scheme');
            document.documentElement.removeAttribute('data-theme');
        } else {
            localStorage.setItem('theme-scheme', scheme);
            document.documentElement.setAttribute('data-theme', scheme);
        }
        this._notifyThemeChanged();
    },
    getScheme: function () {
        return localStorage.getItem('theme-scheme') || 'zinc';
    },
    setRadius: function (radius) {
        localStorage.setItem('theme-radius', radius);
        document.documentElement.style.setProperty('--radius', radius + 'rem');
        this._notifyThemeChanged();
    },
    getRadius: function () {
        return localStorage.getItem('theme-radius') !== null ? localStorage.getItem('theme-radius') : '0.75';
    },
    setStyle: function (style) {
        if (style === 'new-york') {
            localStorage.setItem('theme-style', 'new-york');
            document.documentElement.classList.add('style-new-york');
        } else {
            localStorage.removeItem('theme-style');
            document.documentElement.classList.remove('style-new-york');
        }
        this._notifyThemeChanged();
    },
    getStyle: function () {
        return localStorage.getItem('theme-style') || 'default';
    },
    setBaseColor: function (baseColor) {
        if (!baseColor || baseColor === 'slate') {
            localStorage.removeItem('theme-base-color');
            document.documentElement.removeAttribute('data-base-color');
        } else {
            localStorage.setItem('theme-base-color', baseColor);
            document.documentElement.setAttribute('data-base-color', baseColor);
        }
        this._notifyThemeChanged();
    },
    getBaseColor: function () {
        return localStorage.getItem('theme-base-color') || 'slate';
    },
    setMenuColor: function (menuColor) {
        if (!menuColor || menuColor === 'default') {
            localStorage.removeItem('theme-menu-color');
            document.documentElement.style.removeProperty('--color-sidebar');
            document.documentElement.style.removeProperty('--color-sidebar-foreground');
        } else {
            localStorage.setItem('theme-menu-color', menuColor);
            if (menuColor === 'dark') {
                document.documentElement.style.setProperty('--color-sidebar', 'hsl(220 13% 10%)');
                document.documentElement.style.setProperty('--color-sidebar-foreground', 'hsl(0 0% 95%)');
            } else if (menuColor === 'light') {
                document.documentElement.style.setProperty('--color-sidebar', 'hsl(0 0% 100%)');
                document.documentElement.style.setProperty('--color-sidebar-foreground', 'hsl(220 13% 10%)');
            }
        }
        this._notifyThemeChanged();
    },
    getMenuColor: function () {
        return localStorage.getItem('theme-menu-color') || 'default';
    },
    setMenuAccent: function (accent) {
        if (!accent || accent === 'subtle') {
            localStorage.removeItem('theme-menu-accent');
            document.documentElement.removeAttribute('data-menu-accent');
        } else {
            localStorage.setItem('theme-menu-accent', accent);
            document.documentElement.setAttribute('data-menu-accent', accent);
        }
        this._notifyThemeChanged();
    },
    getMenuAccent: function () {
        return localStorage.getItem('theme-menu-accent') || 'subtle';
    },
    setFont: function (fontValue, fontCss) {
        if (!fontValue || fontValue === 'system') {
            localStorage.removeItem('theme-font');
            localStorage.removeItem('theme-font-css');
        } else {
            localStorage.setItem('theme-font', fontValue);
            localStorage.setItem('theme-font-css', fontCss);
        }
        var el = document.getElementById('lumeo-font-override');
        if (!el) {
            el = document.createElement('style');
            el.id = 'lumeo-font-override';
            document.head.appendChild(el);
        }
        el.textContent = fontCss;
        this._notifyThemeChanged();
    },
    getFont: function () {
        return localStorage.getItem('theme-font') || 'system';
    },
    setDirection: function (dir) {
        var normalized = dir === 'rtl' ? 'rtl' : 'ltr';
        localStorage.setItem('lumeo.direction', normalized);
        document.documentElement.setAttribute('dir', normalized);
        this._notifyThemeChanged();
    },
    getDirection: function () {
        var attr = document.documentElement.getAttribute('dir');
        if (attr === 'rtl' || attr === 'ltr') return attr;
        var stored = localStorage.getItem('lumeo.direction');
        return stored === 'rtl' ? 'rtl' : 'ltr';
    },
    copyText: function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text);
        }
        return Promise.resolve();
    },
    resetAll: function () {
        localStorage.removeItem('theme-radius');
        localStorage.removeItem('theme-style');
        localStorage.removeItem('theme-base-color');
        localStorage.removeItem('theme-menu-color');
        localStorage.removeItem('theme-menu-accent');
        localStorage.removeItem('theme-font');
        localStorage.removeItem('theme-font-css');
        document.documentElement.style.removeProperty('--radius');
        document.documentElement.classList.remove('style-new-york');
        document.documentElement.removeAttribute('data-base-color');
        document.documentElement.style.removeProperty('--color-sidebar');
        document.documentElement.style.removeProperty('--color-sidebar-foreground');
        document.documentElement.removeAttribute('data-menu-accent');
        var el = document.getElementById('lumeo-font-override');
        if (el) el.textContent = '';
    }
};
themeManager.init();
