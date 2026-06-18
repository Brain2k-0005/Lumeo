// Lumeo CodeEditor — CodeMirror 6 wrapper.
//
// Why CodeMirror over Monaco:
//   - Monaco WASM bundle is ~5MB, exceeds Cloudflare Pages free tier 25MB
//     asset ceiling once you stack a few satellite packages.
//   - CodeMirror 6 is modular ESM; the core view+state is ~150KB and each
//     `@codemirror/lang-*` pack is 5–30KB. We import only the language the
//     consumer asks for, so a JSON-only page never downloads the Python pack.
//
// Module loading strategy
// -----------------------
//   - Core (`@codemirror/state`, `@codemirror/view`, `@codemirror/language`,
//     `@codemirror/commands`, `@codemirror/search`, `@codemirror/autocomplete`)
//     is dynamic-imported once per ESM base on first init, cached in
//     `_corePromiseByBase`.
//   - Language packs are lazy-loaded per `language` arg, cached per base in
//     `_langCacheByBase` so switching back to a language is instant.
//   - Themes: the One Dark theme is dynamic-imported when needed; the light
//     theme is CodeMirror's built-in default.
//   - All imports go through `https://esm.sh/` — CDN already serves these
//     packages as ES modules with proper HTTP caching headers.
//
// Theme="auto" detects `document.documentElement.classList.contains('dark')`
// at init time and re-evaluates via a MutationObserver on the html element's
// class attribute, so Lumeo's ThemeSwitcher flips CodeMirror instantly.

// ESM base — overridable three ways, in precedence order:
//   1. the per-component `esmBase` option (C# `EsmBase` parameter) — wins,
//   2. the global `window.lumeoCdn.codeMirrorBase`,
//   3. the public esm.sh CDN.
// All three let airgapped / self-hosted / strict-CSP consumers swap esm.sh for
// their own ESM mirror directory. `_esmBase` is resolved on first init and
// reused for every later import so the global core/lang/theme caches stay keyed
// to a single origin.
function _cdn(key, fallback) {
    return (typeof window !== 'undefined' && window.lumeoCdn && window.lumeoCdn[key]) || fallback;
}
const DEFAULT_ESM = _cdn('codeMirrorBase', 'https://esm.sh').replace(/\/$/, '');
let _esmBase = DEFAULT_ESM;
function _resolveBase(options) {
    const fromOpts = options && typeof options.esmBase === 'string' ? options.esmBase.trim() : '';
    if (fromOpts) _esmBase = fromOpts.replace(/\/$/, '');
    return _esmBase;
}
const CM_VERSION = '6';       // major; esm.sh resolves to latest 6.x
const LANG_VERSION = '6';     // language packs

// Instances keyed by elementId.
const _instances = new Map();

// Module caches are keyed by RESOLVED ESM base: two editors pointing at
// different bases (esm.sh vs a self-hosted mirror) must never share module
// instances — CodeMirror rejects extensions built from a different copy of
// @codemirror/state, so a cross-origin cache hit breaks the editor.
const _corePromiseByBase = new Map();      // base -> Promise<core>
const _langCacheByBase = new Map();        // base -> Map<langKey, Promise<ext>>
const _oneDarkPromiseByBase = new Map();   // base -> Promise<ext>
const _minimapPromiseByBase = new Map();   // base -> Promise<ext|null>

async function loadCore(base = _esmBase) {
    if (_corePromiseByBase.has(base)) return _corePromiseByBase.get(base);
    const promise = (async () => {
        const [state, view, language, commands, search, autocomplete] = await Promise.all([
            import(`${base}/@codemirror/state@${CM_VERSION}`),
            import(`${base}/@codemirror/view@${CM_VERSION}`),
            import(`${base}/@codemirror/language@${CM_VERSION}`),
            import(`${base}/@codemirror/commands@${CM_VERSION}`),
            import(`${base}/@codemirror/search@${CM_VERSION}`),
            import(`${base}/@codemirror/autocomplete@${CM_VERSION}`),
        ]);
        return { state, view, language, commands, search, autocomplete };
    })();
    _corePromiseByBase.set(base, promise);
    return promise;
}

// Maps the user-facing language string to (npm pkg, factory fn name on its exports).
// `factory` is the function CodeMirror's lang packs export to build the LanguageSupport;
// most are named after the language (`javascript`, `python`, …); a few are different.
const LANG_MAP = {
    csharp:     { pkg: '@codemirror/legacy-modes/mode/clike', mode: 'csharp', legacy: true },
    javascript: { pkg: '@codemirror/lang-javascript', factory: 'javascript' },
    typescript: { pkg: '@codemirror/lang-javascript', factory: 'javascript', opts: { typescript: true } },
    html:       { pkg: '@codemirror/lang-html', factory: 'html' },
    css:        { pkg: '@codemirror/lang-css', factory: 'css' },
    json:       { pkg: '@codemirror/lang-json', factory: 'json' },
    markdown:   { pkg: '@codemirror/lang-markdown', factory: 'markdown' },
    xml:        { pkg: '@codemirror/lang-xml', factory: 'xml' },
    sql:        { pkg: '@codemirror/lang-sql', factory: 'sql' },
    python:     { pkg: '@codemirror/lang-python', factory: 'python' },
    plaintext:  null, // no language extension
};

async function loadLanguage(language, base = _esmBase) {
    const key = (language || 'plaintext').toLowerCase();
    const spec = LANG_MAP[key];
    if (!spec) return null;

    let langCache = _langCacheByBase.get(base);
    if (!langCache) {
        langCache = new Map();
        _langCacheByBase.set(base, langCache);
    }
    if (langCache.has(key)) return langCache.get(key);

    const promise = (async () => {
        const core = await loadCore(base);
        if (spec.legacy) {
            // C# (and other clike dialects) live in @codemirror/legacy-modes — they wrap
            // CodeMirror 5 modes via StreamLanguage. Bundle is small (~10KB) and saves
            // us pulling a separate tree-sitter grammar for an admin-form-grade editor.
            const [legacyLang, clike] = await Promise.all([
                import(`${base}/@codemirror/language@${CM_VERSION}`),
                import(`${base}/@codemirror/legacy-modes@${LANG_VERSION}/mode/clike`),
            ]);
            const StreamLanguage = legacyLang.StreamLanguage;
            const mode = clike[spec.mode] || clike.default?.[spec.mode];
            if (!StreamLanguage || !mode) return null;
            return StreamLanguage.define(mode);
        }

        const mod = await import(`${base}/${spec.pkg}@${LANG_VERSION}`);
        const fn = mod[spec.factory] || mod.default?.[spec.factory] || mod.default;
        if (typeof fn !== 'function') return null;
        return spec.opts ? fn(spec.opts) : fn();
    })();

    langCache.set(key, promise);
    return promise;
}

async function loadOneDark(base = _esmBase) {
    if (_oneDarkPromiseByBase.has(base)) return _oneDarkPromiseByBase.get(base);
    const promise = (async () => {
        const mod = await import(`${base}/@codemirror/theme-one-dark@${CM_VERSION}`);
        return mod.oneDark || mod.default?.oneDark || mod.default;
    })();
    _oneDarkPromiseByBase.set(base, promise);
    return promise;
}

// Minimap — CodeMirror 6 has no built-in minimap, so we lazily pull the
// community `@replit/codemirror-minimap` extension from the same ESM base.
// Cached after first load. Returns null (and the editor renders without a
// minimap) if the import fails — e.g. offline with no self-hosted mirror.
async function loadMinimap(base = _esmBase) {
    if (_minimapPromiseByBase.has(base)) return _minimapPromiseByBase.get(base);
    const promise = (async () => {
        try {
            const mod = await import(`${base}/@replit/codemirror-minimap`);
            return mod.showMinimap || mod.default?.showMinimap || mod.default || null;
        } catch (_) {
            // Reset so a later toggle can retry (e.g. network came back).
            _minimapPromiseByBase.delete(base);
            return null;
        }
    })();
    _minimapPromiseByBase.set(base, promise);
    return promise;
}

function isPageDark() {
    try {
        return document.documentElement.classList.contains('dark');
    } catch {
        return false;
    }
}

function resolveDark(theme) {
    if (theme === 'dark') return true;
    if (theme === 'light') return false;
    // auto
    return isPageDark();
}

async function buildExtensions(opts, core, base = _esmBase) {
    const { state, view, language, commands, search, autocomplete } = core;
    const exts = [];

    if (opts.lineNumbers) exts.push(view.lineNumbers());
    exts.push(view.highlightActiveLineGutter());
    exts.push(view.highlightSpecialChars());
    exts.push(commands.history());
    exts.push(language.foldGutter());
    exts.push(view.drawSelection());
    exts.push(view.dropCursor());
    exts.push(state.EditorState.allowMultipleSelections.of(true));
    exts.push(language.indentOnInput());
    exts.push(language.bracketMatching());
    exts.push(autocomplete.closeBrackets());
    exts.push(autocomplete.autocompletion());
    exts.push(view.rectangularSelection());
    exts.push(view.crosshairCursor());
    exts.push(view.highlightActiveLine());
    exts.push(search.highlightSelectionMatches());

    exts.push(view.keymap.of([
        ...autocomplete.closeBracketsKeymap,
        ...commands.defaultKeymap,
        ...search.searchKeymap,
        ...commands.historyKeymap,
        ...language.foldKeymap,
        ...autocomplete.completionKeymap,
    ]));

    // Tab size + (optional) hard-tab indent unit.
    exts.push(state.EditorState.tabSize.of(opts.tabSize || 4));
    if (opts.useTabs) {
        exts.push(language.indentUnit.of('\t'));
    } else {
        exts.push(language.indentUnit.of(' '.repeat(opts.tabSize || 4)));
    }

    if (opts.wordWrap === 'on') exts.push(view.EditorView.lineWrapping);

    if (opts.placeholder) exts.push(view.placeholder(opts.placeholder));

    // ReadOnly: also disable editing so contenteditable stops capturing focus.
    if (opts.readOnly) {
        exts.push(state.EditorState.readOnly.of(true));
        exts.push(view.EditorView.editable.of(false));
    }

    // Language extension (lazy).
    const langExt = await loadLanguage(opts.language, base);
    if (langExt) exts.push(langExt);

    // Theme.
    if (resolveDark(opts.theme)) {
        const oneDark = await loadOneDark(base);
        if (oneDark) exts.push(oneDark);
    }

    // Minimap (lazy, optional). Degrades to no-minimap if the package can't load.
    if (opts.minimap) {
        const showMinimap = await loadMinimap(base);
        if (showMinimap && typeof showMinimap.compute === 'function') {
            exts.push(showMinimap.compute([], () => ({
                create: () => {
                    const dom = document.createElement('div');
                    return { dom };
                },
                displayText: 'characters',
                showOverlay: 'always',
            })));
        }
    }

    return exts;
}

export async function init(elementId, options, dotNetRef) {
    const host = document.getElementById(elementId);
    if (!host) {
        console.warn(`[Lumeo.CodeEditor] host element not found: ${elementId}`);
        return;
    }

    // Wipe any previous content (HMR / re-init safety).
    host.innerHTML = '';

    // Resolve the per-instance ESM base (C# EsmBase) before any import, and
    // thread it through every loader so this editor's modules all come from the
    // same origin (and are cached under that base).
    const base = _resolveBase(options);

    const core = await loadCore(base);
    const { state, view } = core;

    const exts = await buildExtensions(options, core, base);

    // Change listener — fire dotNetRef.OnEditorChange with the latest doc on every edit.
    // CodeMirror's `EditorView.updateListener` fires synchronously per transaction;
    // we debounce to 60ms to match how RichTextEditor (TipTap) handles input — keeps
    // C#-side state updates off the keystroke critical path.
    let debounceTimer = null;
    const debouncedNotify = (doc) => {
        if (debounceTimer) clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            try {
                dotNetRef.invokeMethodAsync('OnEditorChange', doc);
            } catch (e) {
                // Component disposed — observer may still fire briefly.
            }
        }, 60);
    };

    const listener = view.EditorView.updateListener.of((update) => {
        if (update.docChanged) {
            debouncedNotify(update.state.doc.toString());
        }
    });

    const startState = state.EditorState.create({
        doc: options.value || '',
        extensions: [...exts, listener],
    });

    const editorView = new view.EditorView({
        state: startState,
        parent: host,
    });

    // Theme="auto": watch the page's <html class="dark"> toggle and rebuild the
    // theme leg of the config without rebuilding the whole state (preserves
    // selection + scroll position). We track the current dark-mode state so we
    // only swap when it actually changes.
    let themeObserver = null;
    let lastDark = resolveDark(options.theme);
    if (options.theme === 'auto') {
        themeObserver = new MutationObserver(async () => {
            const nextDark = isPageDark();
            if (nextDark === lastDark) return;
            lastDark = nextDark;
            await applyTheme(elementId, options.theme);
        });
        themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    }

    _instances.set(elementId, {
        view: editorView,
        dotNetRef,
        options: { ...options },
        base,
        themeObserver,
        debounceTimer: () => debounceTimer,
    });
}

async function rebuild(elementId) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    const core = await loadCore(inst.base);
    const exts = await buildExtensions(inst.options, core, inst.base);
    // Track the change listener: must re-attach so doc changes still propagate.
    const listener = core.view.EditorView.updateListener.of((update) => {
        if (update.docChanged) {
            try {
                inst.dotNetRef.invokeMethodAsync('OnEditorChange', update.state.doc.toString());
            } catch {}
        }
    });
    inst.view.setState(core.state.EditorState.create({
        doc: inst.view.state.doc,
        selection: inst.view.state.selection,
        extensions: [...exts, listener],
    }));
}

export async function setValue(elementId, value) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    const current = inst.view.state.doc.toString();
    if (current === value) return;
    inst.view.dispatch({
        changes: { from: 0, to: inst.view.state.doc.length, insert: value || '' },
    });
}

export async function setLanguage(elementId, language) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    inst.options.language = language;
    await rebuild(elementId);
}

async function applyTheme(elementId, theme) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    inst.options.theme = theme;
    await rebuild(elementId);
}

export async function setTheme(elementId, theme) {
    await applyTheme(elementId, theme);
    // If the consumer switches away from auto, drop the MutationObserver;
    // if they switch INTO auto we set one up.
    const inst = _instances.get(elementId);
    if (!inst) return;
    if (theme !== 'auto' && inst.themeObserver) {
        inst.themeObserver.disconnect();
        inst.themeObserver = null;
    } else if (theme === 'auto' && !inst.themeObserver) {
        let lastDark = isPageDark();
        inst.themeObserver = new MutationObserver(async () => {
            const nextDark = isPageDark();
            if (nextDark === lastDark) return;
            lastDark = nextDark;
            await applyTheme(elementId, 'auto');
        });
        inst.themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    }
}

export async function setReadOnly(elementId, readOnly) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    inst.options.readOnly = readOnly;
    await rebuild(elementId);
}

export async function setMinimap(elementId, minimap) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    if (inst.options.minimap === minimap) return;
    inst.options.minimap = minimap;
    // rebuild() re-runs buildExtensions, which adds/drops the minimap extension
    // while preserving doc + selection (it copies them into the new state).
    await rebuild(elementId);
}

export async function destroy(elementId) {
    const inst = _instances.get(elementId);
    if (!inst) return;
    // Cancel any pending debounced OnEditorChange round-trip before tearing
    // down the editor. Without this, a setTimeout scheduled within the
    // last 60 ms before destroy still fires, invokes the (about-to-be-)
    // disposed dotNetRef and lands in the catch — harmless but wastes a
    // round-trip and leaves a timer hanging until it resolves.
    try {
        const pending = inst.debounceTimer?.();
        if (pending) clearTimeout(pending);
    } catch {}
    try { inst.themeObserver?.disconnect(); } catch {}
    try { inst.view.destroy(); } catch {}
    _instances.delete(elementId);
}
