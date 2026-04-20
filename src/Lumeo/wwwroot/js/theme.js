window.themeManager = {
    init: function () {
        // Dark mode
        const mode = localStorage.getItem('theme-mode') || localStorage.getItem('theme');
        if (mode === 'dark' || (!mode && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        // Color scheme
        const scheme = localStorage.getItem('theme-scheme');
        if (scheme && scheme !== 'zinc') {
            document.documentElement.setAttribute('data-theme', scheme);
        }
        // Radius
        const radius = localStorage.getItem('theme-radius');
        if (radius !== null) {
            document.documentElement.style.setProperty('--radius', radius + 'rem');
        }
        // Style
        const style = localStorage.getItem('theme-style');
        if (style === 'new-york') {
            document.documentElement.classList.add('style-new-york');
        }
        // Base color
        const baseColor = localStorage.getItem('theme-base-color');
        if (baseColor && baseColor !== 'slate') {
            document.documentElement.setAttribute('data-base-color', baseColor);
        }
        // Menu accent
        const menuAccent = localStorage.getItem('theme-menu-accent');
        if (menuAccent && menuAccent !== 'subtle') {
            document.documentElement.setAttribute('data-menu-accent', menuAccent);
        }
        // Menu color
        const menuColor = localStorage.getItem('theme-menu-color');
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
        // Font
        const fontCss = localStorage.getItem('theme-font-css');
        if (fontCss) {
            var el = document.getElementById('lumeo-font-override');
            if (!el) {
                el = document.createElement('style');
                el.id = 'lumeo-font-override';
                document.head.appendChild(el);
            }
            el.textContent = fontCss;
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
