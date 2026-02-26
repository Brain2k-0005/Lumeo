window.themeManager = {
    init: function () {
        const mode = localStorage.getItem('theme-mode') || localStorage.getItem('theme');
        if (mode === 'dark' || (!mode && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        const scheme = localStorage.getItem('theme-scheme');
        if (scheme && scheme !== 'orange') {
            document.documentElement.setAttribute('data-theme', scheme);
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
    },
    setScheme: function (scheme) {
        if (!scheme || scheme === 'orange') {
            localStorage.removeItem('theme-scheme');
            document.documentElement.removeAttribute('data-theme');
        } else {
            localStorage.setItem('theme-scheme', scheme);
            document.documentElement.setAttribute('data-theme', scheme);
        }
    },
    getScheme: function () {
        return localStorage.getItem('theme-scheme') || 'orange';
    }
};
themeManager.init();
