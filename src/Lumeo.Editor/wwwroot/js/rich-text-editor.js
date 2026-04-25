// Lumeo RichTextEditor — TipTap v2.10.x wrapper with Notion-tier features.
//
// Loaded on demand. All TipTap modules are imported from esm.sh once and cached
// across instances. Includes: starter-kit, mention, table, code-block-lowlight,
// image, link, placeholder, typography, task list/item, plus a custom slash
// command suggestion plugin, drag-handle plugin, bubble menu plugin, and smart
// paste handling (Word cleanup, autolink, basic Markdown).
//
// Public surface (all on `window.lumeoRichTextEditor` and module export `rte`):
//   init, setHtml/setContent, getHtml, focus, blur, executeCommand/command,
//   getActive, setDisabled, destroy, promptLink, triggerQueryResults

const TIPTAP_VERSION = '2.10.4';

let _tiptapPromise = null;

async function loadTiptap() {
    if (_tiptapPromise) return _tiptapPromise;
    const v = TIPTAP_VERSION;
    _tiptapPromise = (async () => {
        const [
            core, pmState, pmView,
            starterKit, link, underline, placeholder, typography,
            mention, suggestion,
            table, tableRow, tableCell, tableHeader,
            image, taskList, taskItem,
            codeBlockLowlight, lowlightMod,
            jsHl, tsHl, pyHl, cssHl, htmlHl, jsonHl, bashHl,
        ] = await Promise.all([
            import(`https://esm.sh/@tiptap/core@${v}`),
            import('https://esm.sh/@tiptap/pm@2.10.4/state'),
            import('https://esm.sh/@tiptap/pm@2.10.4/view'),
            import(`https://esm.sh/@tiptap/starter-kit@${v}`),
            import(`https://esm.sh/@tiptap/extension-link@${v}`),
            import(`https://esm.sh/@tiptap/extension-underline@${v}`),
            import(`https://esm.sh/@tiptap/extension-placeholder@${v}`),
            import(`https://esm.sh/@tiptap/extension-typography@${v}`),
            import(`https://esm.sh/@tiptap/extension-mention@${v}`),
            import(`https://esm.sh/@tiptap/suggestion@${v}`),
            import(`https://esm.sh/@tiptap/extension-table@${v}`),
            import(`https://esm.sh/@tiptap/extension-table-row@${v}`),
            import(`https://esm.sh/@tiptap/extension-table-cell@${v}`),
            import(`https://esm.sh/@tiptap/extension-table-header@${v}`),
            import(`https://esm.sh/@tiptap/extension-image@${v}`),
            import(`https://esm.sh/@tiptap/extension-task-list@${v}`),
            import(`https://esm.sh/@tiptap/extension-task-item@${v}`),
            import(`https://esm.sh/@tiptap/extension-code-block-lowlight@${v}`),
            import('https://esm.sh/lowlight@3'),
            import('https://esm.sh/highlight.js@11/lib/languages/javascript').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/typescript').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/python').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/css').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/xml').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/json').catch(() => null),
            import('https://esm.sh/highlight.js@11/lib/languages/bash').catch(() => null),
        ]);

        // Build a lowlight registry with a few common languages.
        const createLowlight = lowlightMod.createLowlight || lowlightMod.default?.createLowlight;
        const lowlight = createLowlight ? createLowlight() : (lowlightMod.lowlight || lowlightMod.default);
        const reg = (name, mod) => {
            try {
                if (mod && lowlight?.register) {
                    lowlight.register(name, mod.default || mod);
                }
            } catch (_) { /* ignore */ }
        };
        reg('javascript', jsHl); reg('js', jsHl);
        reg('typescript', tsHl); reg('ts', tsHl);
        reg('python', pyHl); reg('py', pyHl);
        reg('css', cssHl);
        reg('html', htmlHl); reg('xml', htmlHl);
        reg('json', jsonHl);
        reg('bash', bashHl); reg('sh', bashHl);

        const pick = (m, name) => m.default || m[name] || m;

        return {
            core,
            Editor: core.Editor,
            Extension: core.Extension,
            Node: core.Node,
            mergeAttributes: core.mergeAttributes,
            PluginKey: pmState.PluginKey,
            Plugin: pmState.Plugin,
            Decoration: pmView.Decoration,
            DecorationSet: pmView.DecorationSet,
            StarterKit: pick(starterKit, 'StarterKit'),
            Link: pick(link, 'Link'),
            Underline: pick(underline, 'Underline'),
            Placeholder: pick(placeholder, 'Placeholder'),
            Typography: pick(typography, 'Typography'),
            Mention: pick(mention, 'Mention'),
            Suggestion: pick(suggestion, 'Suggestion'),
            Table: pick(table, 'Table'),
            TableRow: pick(tableRow, 'TableRow'),
            TableCell: pick(tableCell, 'TableCell'),
            TableHeader: pick(tableHeader, 'TableHeader'),
            Image: pick(image, 'Image'),
            TaskList: pick(taskList, 'TaskList'),
            TaskItem: pick(taskItem, 'TaskItem'),
            CodeBlockLowlight: pick(codeBlockLowlight, 'CodeBlockLowlight'),
            lowlight,
        };
    })();
    return _tiptapPromise;
}

const instances = new Map();
const pendingTriggerQueries = new Map(); // key = `${id}:${triggerChar}:${seq}` -> resolve
let _querySeq = 0;

function makeId() {
    return 'rte-' + Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}

function safeInvoke(dotNetRef, name, ...args) {
    try {
        return dotNetRef.invokeMethodAsync(name, ...args);
    } catch (_) {
        return Promise.resolve(null);
    }
}

// ---------------------------------------------------------------------------
// Suggestion-list popup (used by mentions, slash commands, custom triggers).
// Renders a styled floating list near the trigger caret. Keyboard nav is
// handled by TipTap's Suggestion plugin via the renderer's onKeyDown hook.
// ---------------------------------------------------------------------------

function createSuggestionRenderer(label) {
    let root = null;
    let items = [];
    let selectedIndex = 0;
    let command = () => {};
    let editor = null;

    function ensureRoot() {
        if (root) return root;
        root = document.createElement('div');
        root.className = 'lumeo-rte-suggestion';
        root.setAttribute('role', 'listbox');
        root.setAttribute('aria-label', label || 'Suggestions');
        root.style.cssText = [
            'position:fixed', 'z-index:9999',
            'min-width:200px', 'max-width:340px', 'max-height:280px',
            'overflow-y:auto', 'border-radius:0.5rem',
            'border:1px solid var(--color-border)',
            'background:var(--color-popover)', 'color:var(--color-popover-foreground)',
            'box-shadow:0 10px 25px -5px rgb(0 0 0 / 0.15), 0 8px 10px -6px rgb(0 0 0 / 0.1)',
            'padding:0.25rem', 'font-size:0.875rem',
        ].join(';');
        document.body.appendChild(root);
        return root;
    }

    function render() {
        ensureRoot();
        if (!items || items.length === 0) {
            root.innerHTML = '<div style="padding:0.5rem 0.625rem;color:var(--color-muted-foreground);font-size:0.8125rem">No results</div>';
            return;
        }
        const html = items.map((it, i) => {
            const sel = i === selectedIndex;
            const subtitle = it.subtitle ? `<div style="font-size:0.75rem;color:var(--color-muted-foreground);line-height:1.1">${escapeHtml(it.subtitle)}</div>` : '';
            const icon = it.icon ? `<span style="display:inline-flex;align-items:center;width:1rem;height:1rem;color:var(--color-muted-foreground);margin-right:0.5rem">${iconSvg(it.icon)}</span>` : '';
            const bg = sel ? 'background:var(--color-accent);color:var(--color-accent-foreground);' : '';
            return `<div role="option" data-index="${i}" aria-selected="${sel}" style="display:flex;align-items:flex-start;padding:0.4rem 0.55rem;border-radius:0.375rem;cursor:pointer;${bg}">
                ${icon}
                <div style="flex:1;min-width:0">
                    <div style="font-weight:500">${escapeHtml(it.label || it.id || '')}</div>
                    ${subtitle}
                </div>
            </div>`;
        }).join('');
        root.innerHTML = html;
        root.querySelectorAll('[data-index]').forEach(el => {
            el.addEventListener('mousedown', (e) => {
                e.preventDefault();
                const idx = Number(el.getAttribute('data-index'));
                pickItem(idx);
            });
            el.addEventListener('mouseenter', () => {
                selectedIndex = Number(el.getAttribute('data-index'));
                render();
            });
        });
    }

    function position(props) {
        ensureRoot();
        const rect = props.clientRect && props.clientRect();
        if (!rect) {
            root.style.display = 'none';
            return;
        }
        root.style.display = 'block';
        const top = rect.bottom + 6;
        const left = rect.left;
        const vw = window.innerWidth, vh = window.innerHeight;
        root.style.top = Math.min(top, vh - 320) + 'px';
        root.style.left = Math.min(left, vw - 360) + 'px';
    }

    function pickItem(i) {
        const item = items[i];
        if (!item) return;
        command(item);
    }

    function destroy() {
        if (root) {
            try { root.remove(); } catch (_) {}
            root = null;
        }
    }

    return {
        onStart(props) {
            editor = props.editor;
            items = props.items || [];
            selectedIndex = 0;
            command = props.command;
            render();
            position(props);
        },
        onUpdate(props) {
            items = props.items || [];
            selectedIndex = Math.min(selectedIndex, Math.max(0, items.length - 1));
            command = props.command;
            render();
            position(props);
        },
        onKeyDown(props) {
            const e = props.event;
            if (e.key === 'ArrowDown') {
                selectedIndex = (selectedIndex + 1) % Math.max(1, items.length);
                render();
                return true;
            }
            if (e.key === 'ArrowUp') {
                selectedIndex = (selectedIndex - 1 + Math.max(1, items.length)) % Math.max(1, items.length);
                render();
                return true;
            }
            if (e.key === 'Enter' || e.key === 'Tab') {
                pickItem(selectedIndex);
                return true;
            }
            if (e.key === 'Escape') {
                destroy();
                return true;
            }
            return false;
        },
        onExit() {
            destroy();
        },
    };
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

// Tiny inline SVG fallback for suggestion icons. Real toolbars use Blazicons;
// this is only for the floating dropdown which is fully JS-rendered.
function iconSvg(name) {
    const paths = {
        Heading1: '<path d="M4 12h8M4 6v12M12 6v12M17 12l3-2v8"/>',
        Heading2: '<path d="M4 12h8M4 6v12M12 6v12M21 18h-4c0-4 4-3 4-6 0-1.5-2-2.5-4-1"/>',
        Heading3: '<path d="M4 12h8M4 6v12M12 6v12M17.5 10.5c1.7-1 3.5 0 3.5 1.5a2 2 0 0 1-2 2M21 16a2 2 0 0 1-2 2c-1.5 0-3-1-3-2"/>',
        List: '<path d="M3 12h.01M3 6h.01M3 18h.01M8 6h13M8 12h13M8 18h13"/>',
        ListOrdered: '<path d="M10 6h11M10 12h11M10 18h11M4 6h1v4M4 10h2M6 18H4c0-1 2-2 2-3s-1-1.5-2-1"/>',
        ListChecks: '<path d="m3 17 2 2 4-4M3 7l2 2 4-4M13 6h8M13 12h8M13 18h8"/>',
        Quote: '<path d="M3 21c3 0 7-1 7-8V5c0-1.25-.756-2-2-2H4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2h2c1 0 1 1 1 2v1c0 2-3 2-3 2v3zM15 21c3 0 7-1 7-8V5c0-1.25-.757-2-2-2h-4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2h2c1 0 1 1 1 2v1c0 2-3 2-3 2v3z"/>',
        Code: '<path d="m16 18 6-6-6-6M8 6l-6 6 6 6"/>',
        SquareCode: '<path d="M10 9.5 8 12l2 2.5M14 9.5l2 2.5-2 2.5M3 5v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2z"/>',
        Table: '<path d="M3 3h18v18H3zM3 9h18M3 15h18M9 3v18M15 3v18"/>',
        Image: '<path d="M21 15V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2zM8.5 10.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM21 15l-5-5L5 21"/>',
        Minus: '<path d="M5 12h14"/>',
    };
    const d = paths[name] || '<circle cx="12" cy="12" r="9"/>';
    return `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${d}</svg>`;
}

// ---------------------------------------------------------------------------
// Bubble menu (selection-driven floating toolbar, includes AI dropdown).
// ---------------------------------------------------------------------------

function createBubbleMenu(editor, dotNetRef, instanceId, opts) {
    const root = document.createElement('div');
    root.className = 'lumeo-rte-bubble';
    root.setAttribute('role', 'toolbar');
    root.setAttribute('aria-label', 'Format selection');
    root.style.cssText = [
        'position:fixed', 'z-index:9998', 'display:none',
        'gap:0.125rem', 'padding:0.25rem',
        'border-radius:0.5rem',
        'border:1px solid var(--color-border)',
        'background:var(--color-popover)', 'color:var(--color-popover-foreground)',
        'box-shadow:0 8px 20px -4px rgb(0 0 0 / 0.18)',
    ].join(';');

    const mkBtn = (label, icon, onClick) => {
        const b = document.createElement('button');
        b.type = 'button';
        b.title = label;
        b.setAttribute('aria-label', label);
        b.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:1.75rem;height:1.75rem;border-radius:0.375rem;background:transparent;border:0;color:inherit;cursor:pointer';
        b.innerHTML = iconSvg(icon);
        b.addEventListener('mousedown', e => e.preventDefault());
        b.addEventListener('click', onClick);
        b.addEventListener('mouseenter', () => { b.style.background = 'var(--color-accent)'; });
        b.addEventListener('mouseleave', () => { b.style.background = 'transparent'; });
        return b;
    };

    root.appendChild(mkBtn('Bold', 'Code', () => editor.chain().focus().toggleBold().run()));
    root.appendChild(mkBtn('Italic', 'Code', () => editor.chain().focus().toggleItalic().run()));
    root.appendChild(mkBtn('Link', 'Code', () => {
        const url = window.prompt('Link URL', 'https://');
        if (url) editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
    }));

    if (opts && opts.enableAiActions && dotNetRef) {
        const ai = mkBtn('AI actions', 'Code', async () => {
            const sel = editor.state.doc.textBetween(editor.state.selection.from, editor.state.selection.to, ' ');
            if (!sel) return;
            // Default to "Improve" via the .NET callback. Consumers can render
            // a richer menu via the C# AiActionMenu component; this is a quick
            // single-click affordance.
            try {
                const result = await dotNetRef.invokeMethodAsync('OnAiAction', 'improve', sel);
                if (result) {
                    editor.chain().focus().deleteSelection().insertContent(result).run();
                }
            } catch (_) {}
        });
        root.appendChild(ai);
    }

    document.body.appendChild(root);

    function update() {
        const { from, to, empty } = editor.state.selection;
        if (empty || !editor.isFocused) {
            root.style.display = 'none';
            return;
        }
        const start = editor.view.coordsAtPos(from);
        const end = editor.view.coordsAtPos(to);
        root.style.display = 'flex';
        const top = Math.max(0, start.top - 44);
        const left = Math.max(8, (start.left + end.left) / 2 - root.offsetWidth / 2);
        root.style.top = top + 'px';
        root.style.left = left + 'px';
    }

    editor.on('selectionUpdate', update);
    editor.on('blur', () => { setTimeout(() => { if (!editor.isFocused) root.style.display = 'none'; }, 200); });
    editor.on('focus', update);

    return {
        update,
        destroy() { try { root.remove(); } catch (_) {} },
    };
}

// ---------------------------------------------------------------------------
// Drag handle plugin: shows a `⋮⋮` handle on the left margin of the hovered
// block. Drag-to-reorder is implemented by setting the dragged block's range
// as the editor selection and letting ProseMirror's native dragstart kick in.
// ---------------------------------------------------------------------------

function createDragHandle(editor) {
    const handle = document.createElement('div');
    handle.className = 'lumeo-rte-drag-handle';
    handle.setAttribute('aria-label', 'Drag block');
    handle.setAttribute('contenteditable', 'false');
    handle.draggable = true;
    handle.style.cssText = [
        'position:absolute', 'display:none', 'width:1.25rem', 'height:1.25rem',
        'align-items:center', 'justify-content:center', 'cursor:grab',
        'color:var(--color-muted-foreground)', 'border-radius:0.25rem',
        'opacity:0', 'transition:opacity 120ms',
        'user-select:none', 'pointer-events:auto', 'z-index:5',
    ].join(';');
    handle.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><circle cx="9" cy="6" r="1.5"/><circle cx="15" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/><circle cx="15" cy="18" r="1.5"/></svg>';

    let activeNodePos = null;
    let activeNodeEl = null;

    const dom = editor.view.dom;
    const wrapper = dom.parentElement;
    if (wrapper && getComputedStyle(wrapper).position === 'static') {
        wrapper.style.position = 'relative';
    }
    (wrapper || document.body).appendChild(handle);

    function blockAt(target) {
        let el = target;
        while (el && el !== dom) {
            if (el.parentElement === dom) return el;
            el = el.parentElement;
        }
        return null;
    }

    dom.addEventListener('mousemove', (e) => {
        const block = blockAt(e.target);
        if (!block) {
            handle.style.opacity = '0';
            handle.style.display = 'none';
            return;
        }
        const rect = block.getBoundingClientRect();
        const wrapRect = (wrapper || document.body).getBoundingClientRect();
        handle.style.display = 'flex';
        handle.style.opacity = '1';
        handle.style.top = (rect.top - wrapRect.top + 4) + 'px';
        handle.style.left = (rect.left - wrapRect.left - 24) + 'px';
        try {
            activeNodePos = editor.view.posAtDOM(block, 0);
            activeNodeEl = block;
        } catch (_) { activeNodePos = null; }
    });

    dom.addEventListener('mouseleave', () => {
        handle.style.opacity = '0';
        setTimeout(() => { handle.style.display = 'none'; }, 200);
    });

    handle.addEventListener('mousedown', (e) => {
        if (activeNodePos == null) return;
        try {
            const $pos = editor.state.doc.resolve(activeNodePos);
            const node = $pos.nodeAfter || $pos.parent;
            const start = $pos.before($pos.depth);
            const end = start + (node ? node.nodeSize : 0);
            editor.chain().focus().setNodeSelection(start).run();
        } catch (_) {}
    });

    handle.addEventListener('dragstart', (e) => {
        if (!activeNodeEl) return;
        // Let ProseMirror handle the drop; we need to put the block content
        // on the dataTransfer so DOM-level drop targets get it too.
        try {
            const sel = editor.state.selection;
            const slice = editor.state.doc.slice(sel.from, sel.to);
            const html = activeNodeEl.outerHTML;
            e.dataTransfer.setData('text/html', html);
            e.dataTransfer.setData('text/plain', activeNodeEl.innerText || '');
        } catch (_) {}
    });

    return {
        destroy() { try { handle.remove(); } catch (_) {} },
    };
}

// ---------------------------------------------------------------------------
// Smart paste: handle paste events to clean Word/Google Docs HTML, autolink
// URLs, and convert basic Markdown.
// ---------------------------------------------------------------------------

function smartPasteRule() {
    const seenUrl = /^https?:\/\/\S+$/i;
    return {
        // Returns an editorProps `handlePaste` function compatible with TipTap.
        handlePaste(view, event) {
            const cd = event.clipboardData;
            if (!cd) return false;
            const html = cd.getData('text/html');
            const text = cd.getData('text/plain');

            // Pure URL → let TipTap's link extension autolink-on-paste handle it.
            if (text && seenUrl.test(text.trim()) && !html) return false;

            if (html) {
                const cleaned = sanitizeHtml(html);
                if (cleaned !== html) {
                    const slice = htmlToSlice(view, cleaned);
                    if (slice) {
                        view.dispatch(view.state.tr.replaceSelection(slice));
                        return true;
                    }
                }
                return false;
            }

            // Markdown-ish text? Convert simple cases.
            if (text && /(^|\n)(#{1,3}\s|[-*]\s|\d+\.\s|>\s|```)/.test(text)) {
                const md = miniMarkdownToHtml(text);
                const slice = htmlToSlice(view, md);
                if (slice) {
                    view.dispatch(view.state.tr.replaceSelection(slice));
                    return true;
                }
            }
            return false;
        }
    };
}

function htmlToSlice(view, html) {
    try {
        const tmp = document.createElement('div');
        tmp.innerHTML = html;
        const parser = view.props && view.props.clipboardParser
            ? view.props.clipboardParser
            : null;
        // ProseMirror DOM parser path
        const pmModel = view.state.schema;
        const DOMParser = pmModel && pmModel.constructor && null; // not directly accessible
        // Easiest: defer to TipTap's view by simulating a paste through clipboard parser
        const slice = view.someProp('clipboardParser', f => f.parseSlice(tmp, { preserveWhitespace: true }))
            || view.someProp('domParser', f => f.parseSlice(tmp));
        return slice || null;
    } catch (_) {
        return null;
    }
}

function sanitizeHtml(html) {
    // Strip Word/Google Docs class+style noise. We keep semantic tags and
    // anchors, but remove inline styles, MS Office namespaces, and empty spans.
    let s = html;
    s = s.replace(/<!--[\s\S]*?-->/g, '');
    s = s.replace(/<\/?o:p[^>]*>/gi, '');
    s = s.replace(/<\/?xml[\s\S]*?>/gi, '');
    s = s.replace(/<style[\s\S]*?<\/style>/gi, '');
    s = s.replace(/<meta[^>]*>/gi, '');
    s = s.replace(/<link[^>]*>/gi, '');
    s = s.replace(/\s(class|style|lang|xml:lang|align|width|height|border|cellspacing|cellpadding|valign|bgcolor)="[^"]*"/gi, '');
    s = s.replace(/\s(class|style|lang|align|width|height)='[^']*'/gi, '');
    s = s.replace(/<span[^>]*>/gi, '<span>');
    s = s.replace(/<span>(\s*)<\/span>/gi, '$1');
    s = s.replace(/<font[^>]*>/gi, '');
    s = s.replace(/<\/font>/gi, '');
    return s;
}

function miniMarkdownToHtml(md) {
    const lines = md.split(/\r?\n/);
    const out = [];
    let inUl = false, inOl = false, inCode = false, codeLang = '';
    const closeLists = () => {
        if (inUl) { out.push('</ul>'); inUl = false; }
        if (inOl) { out.push('</ol>'); inOl = false; }
    };
    for (const raw of lines) {
        const line = raw;
        const fence = line.match(/^```(\w*)$/);
        if (fence) {
            closeLists();
            if (inCode) { out.push('</code></pre>'); inCode = false; }
            else { codeLang = fence[1]; out.push(`<pre><code class="language-${codeLang}">`); inCode = true; }
            continue;
        }
        if (inCode) { out.push(escapeHtml(line)); out.push('\n'); continue; }
        const h = line.match(/^(#{1,3})\s+(.*)/);
        if (h) { closeLists(); out.push(`<h${h[1].length}>${inline(h[2])}</h${h[1].length}>`); continue; }
        if (/^>\s+/.test(line)) { closeLists(); out.push(`<blockquote><p>${inline(line.replace(/^>\s+/, ''))}</p></blockquote>`); continue; }
        if (/^[-*]\s+/.test(line)) {
            if (!inUl) { closeLists(); out.push('<ul>'); inUl = true; }
            out.push(`<li>${inline(line.replace(/^[-*]\s+/, ''))}</li>`);
            continue;
        }
        if (/^\d+\.\s+/.test(line)) {
            if (!inOl) { closeLists(); out.push('<ol>'); inOl = true; }
            out.push(`<li>${inline(line.replace(/^\d+\.\s+/, ''))}</li>`);
            continue;
        }
        closeLists();
        if (line.trim() === '') out.push('');
        else out.push(`<p>${inline(line)}</p>`);
    }
    closeLists();
    if (inCode) out.push('</code></pre>');
    return out.join('');

    function inline(s) {
        s = escapeHtml(s);
        s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
        s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        s = s.replace(/(^|\W)\*([^*]+)\*/g, '$1<em>$2</em>');
        s = s.replace(/(https?:\/\/\S+)/g, '<a href="$1">$1</a>');
        return s;
    }
}

// ---------------------------------------------------------------------------
// Build the slash-command suggestion plugin (uses TipTap's Suggestion utility).
// ---------------------------------------------------------------------------

function buildSuggestionExtension(libs, char, dotNetRef, instanceIdRef, label, getResults) {
    const { Extension, Suggestion, PluginKey } = libs;
    return Extension.create({
        name: `triggerSuggest_${char}`,
        addOptions() {
            return { suggestion: { char, allowSpaces: false, startOfLine: false } };
        },
        addProseMirrorPlugins() {
            return [
                Suggestion({
                    editor: this.editor,
                    char,
                    pluginKey: new PluginKey(`triggerSuggest_${char}`),
                    items: async ({ query }) => {
                        try {
                            const id = instanceIdRef.current;
                            const results = await getResults(id, char, query);
                            return results || [];
                        } catch (_) {
                            return [];
                        }
                    },
                    command: ({ editor, range, props }) => {
                        // Slash command: replace trigger range with the picked block.
                        if (char === '/') {
                            editor.chain().focus().deleteRange(range).run();
                            applySlashCommand(editor, props);
                        } else {
                            // Mention chip: insert a mention node.
                            editor
                                .chain()
                                .focus()
                                .insertContentAt(range, [
                                    { type: 'mention', attrs: { id: props.id, label: props.label } },
                                    { type: 'text', text: ' ' },
                                ])
                                .run();
                        }
                    },
                    render: () => createSuggestionRenderer(label),
                }),
            ];
        },
    });
}

function applySlashCommand(editor, item) {
    const id = item.id || '';
    const chain = editor.chain().focus();
    switch (id) {
        case 'h1': chain.toggleHeading({ level: 1 }).run(); break;
        case 'h2': chain.toggleHeading({ level: 2 }).run(); break;
        case 'h3': chain.toggleHeading({ level: 3 }).run(); break;
        case 'bullet': chain.toggleBulletList().run(); break;
        case 'ordered': chain.toggleOrderedList().run(); break;
        case 'task': chain.toggleTaskList && chain.toggleTaskList().run(); break;
        case 'quote': chain.toggleBlockquote().run(); break;
        case 'code': chain.toggleCodeBlock().run(); break;
        case 'table': chain.insertTable && chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(); break;
        case 'image': {
            // Defer to host: emit an OnImageRequested via dotnet by inserting a
            // sentinel that the toolbar's Image button also uses. Here we just
            // open file picker.
            const inp = document.createElement('input');
            inp.type = 'file';
            inp.accept = 'image/*';
            inp.onchange = () => {
                const f = inp.files && inp.files[0];
                if (!f) return;
                const reader = new FileReader();
                reader.onload = () => {
                    editor.chain().focus().setImage({ src: String(reader.result) }).run();
                };
                reader.readAsDataURL(f);
            };
            inp.click();
            break;
        }
        case 'divider': case 'hr': chain.setHorizontalRule().run(); break;
        default:
            // Unknown id — insert plain text label as fallback.
            chain.insertContent(item.label || '').run();
    }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export const rte = {
    async init(elOrId, dotNetRef, options) {
        const opts = options || {};
        const el = typeof elOrId === 'string' ? document.getElementById(elOrId) : elOrId;
        if (!el) return '';

        // Lazy init: wait until the editor scrolls into view before paying the
        // ~3-5 MB TipTap+lowlight init cost. Massive win on pages with many editors.
        // Skip the wait if the element is already on screen (typical first-paint case).
        const inViewport = (() => {
            const r = el.getBoundingClientRect();
            return r.top < window.innerHeight && r.bottom > 0;
        })();
        if (!inViewport && typeof IntersectionObserver !== 'undefined') {
            await new Promise(resolve => {
                const io = new IntersectionObserver((entries) => {
                    if (entries.some(e => e.isIntersecting)) {
                        io.disconnect();
                        resolve();
                    }
                }, { rootMargin: '200px' });
                io.observe(el);
            });
        }

        const libs = await loadTiptap();

        const id = makeId();
        const idRef = { current: id };

        // ---- Build extension list ----
        const ext = [];

        ext.push(libs.StarterKit.configure({
            // We swap CodeBlock for the lowlight version.
            codeBlock: false,
        }));
        ext.push(libs.Underline);
        ext.push(libs.Link.configure({
            openOnClick: false,
            autolink: true,
            linkOnPaste: true,
            HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' },
        }));
        ext.push(libs.Placeholder.configure({ placeholder: opts.placeholder || 'Start writing…' }));

        if (opts.enableMarkdownShortcuts !== false) {
            ext.push(libs.Typography);
        }

        if (opts.enableCodeBlock !== false && libs.CodeBlockLowlight && libs.lowlight) {
            try {
                ext.push(libs.CodeBlockLowlight.configure({ lowlight: libs.lowlight }));
            } catch (_) { /* fallback: no syntax highlight */ }
        }

        if (opts.enableTables !== false) {
            ext.push(libs.Table.configure({ resizable: true }));
            ext.push(libs.TableRow);
            ext.push(libs.TableHeader);
            ext.push(libs.TableCell);
        }

        if (opts.enableImages !== false) {
            ext.push(libs.Image.configure({ inline: false, allowBase64: true }));
        }

        // Task list / checkboxes
        if (opts.enableTaskList !== false) {
            ext.push(libs.TaskList);
            ext.push(libs.TaskItem.configure({ nested: true }));
        }

        // Mention triggers + slash command, all unified through buildSuggestionExtension.
        const triggers = Array.isArray(opts.mentionTriggers) ? opts.mentionTriggers.slice() : [];
        if (opts.enableSlashCommand) {
            triggers.push({ char: '/', label: 'Insert block' });
        }

        // The Mention node MUST be added if any trigger uses chip rendering.
        const hasMentionChip = triggers.some(t => t.char !== '/');
        if (hasMentionChip) {
            ext.push(libs.Mention.configure({
                HTMLAttributes: {
                    class: 'lumeo-rte-mention',
                },
                renderHTML({ options, node }) {
                    return [
                        'span',
                        libs.mergeAttributes(options.HTMLAttributes, {
                            'data-mention-id': node.attrs.id,
                            style: 'display:inline-block;padding:0 0.25rem;border-radius:0.25rem;background:var(--color-accent);color:var(--color-accent-foreground);font-weight:500',
                        }),
                        `${triggers[0]?.char || '@'}${node.attrs.label ?? node.attrs.id}`,
                    ];
                },
                renderText({ node }) {
                    return `${triggers.find(t => t.char !== '/')?.char || '@'}${node.attrs.label ?? node.attrs.id}`;
                },
            }));
        }

        const queryFn = async (instId, char, query) => {
            // Asks .NET for trigger results.
            try {
                const res = await dotNetRef.invokeMethodAsync('OnTriggerQuery', String(char), query || '');
                return Array.isArray(res) ? res : [];
            } catch (_) {
                // Fallback: built-in slash items only.
                if (char === '/') return defaultSlashItems(query || '');
                return [];
            }
        };

        for (const t of triggers) {
            ext.push(buildSuggestionExtension(libs, t.char, dotNetRef, idRef, t.label, queryFn));
        }

        // ---- Smart paste ----
        const pasteRule = smartPasteRule();

        // Define the editor.
        const editor = new libs.Editor({
            element: el,
            extensions: ext,
            content: opts.initialHtml || opts.content || '',
            editable: opts.editable !== false && !(opts.disabled || opts.readOnly),
            editorProps: {
                handlePaste: pasteRule.handlePaste,
                attributes: {
                    class: 'lumeo-rte-prose prose prose-sm max-w-none p-4 focus:outline-none',
                    spellcheck: 'true',
                },
            },
            onUpdate: ({ editor: ed }) => {
                const html = ed.getHTML();
                if (typeof opts.maxLength === 'number' && opts.maxLength > 0) {
                    if (ed.storage && ed.getText().length > opts.maxLength) {
                        // Soft cap — emit the overflow but consumers can trim.
                    }
                }
                safeInvoke(dotNetRef, 'OnContentUpdate', html);
                safeInvoke(dotNetRef, 'OnUpdate', html);
            },
            onSelectionUpdate: () => {
                safeInvoke(dotNetRef, 'OnSelectionUpdate');
            },
        });

        // ---- Image drop/paste handler ----
        el.addEventListener('drop', async (e) => {
            const files = e.dataTransfer && e.dataTransfer.files;
            if (!files || files.length === 0) return;
            const imgs = [...files].filter(f => f.type.startsWith('image/'));
            if (imgs.length === 0) return;
            e.preventDefault();
            for (const f of imgs) {
                const url = await uploadImage(dotNetRef, f);
                if (url) editor.chain().focus().setImage({ src: url }).run();
            }
        });

        // ---- Bubble menu ----
        const bubble = createBubbleMenu(editor, dotNetRef, id, opts);

        // ---- Drag handle ----
        const drag = createDragHandle(editor);

        instances.set(id, {
            editor, dotNetRef, bubble, drag,
            snapshots: [],
        });
        return id;
    },

    setContent(id, html) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.commands.setContent(html ?? '', false);
    },

    setHtml(id, html) { return this.setContent(id, html); },

    getHtml(id) {
        const entry = instances.get(id);
        if (!entry) return '';
        return entry.editor.getHTML();
    },

    focus(id) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.commands.focus();
    },

    blur(id) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.commands.blur();
    },

    setDisabled(id, disabled) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.setEditable(!disabled);
    },

    executeCommand(id, name, payload) {
        return this.command(id, name, payload);
    },

    command(id, name, ...args) {
        const entry = instances.get(id);
        if (!entry) return;
        const ed = entry.editor;
        const chain = ed.chain().focus();
        switch (name) {
            case 'bold': chain.toggleBold().run(); break;
            case 'italic': chain.toggleItalic().run(); break;
            case 'underline': chain.toggleUnderline().run(); break;
            case 'strike': chain.toggleStrike().run(); break;
            case 'code': chain.toggleCode().run(); break;
            case 'setHeading': chain.toggleHeading({ level: args[0] || 1 }).run(); break;
            case 'setParagraph': chain.setParagraph().run(); break;
            case 'bulletList': chain.toggleBulletList().run(); break;
            case 'orderedList': chain.toggleOrderedList().run(); break;
            case 'taskList': chain.toggleTaskList && chain.toggleTaskList().run(); break;
            case 'blockquote': chain.toggleBlockquote().run(); break;
            case 'codeBlock': chain.toggleCodeBlock().run(); break;
            case 'hr': chain.setHorizontalRule().run(); break;
            case 'insertTable':
                chain.insertTable && chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run();
                break;
            case 'addColumnAfter': chain.addColumnAfter && chain.addColumnAfter().run(); break;
            case 'deleteColumn': chain.deleteColumn && chain.deleteColumn().run(); break;
            case 'addRowAfter': chain.addRowAfter && chain.addRowAfter().run(); break;
            case 'deleteRow': chain.deleteRow && chain.deleteRow().run(); break;
            case 'deleteTable': chain.deleteTable && chain.deleteTable().run(); break;
            case 'image': {
                const url = args[0];
                if (url) chain.setImage({ src: url }).run();
                break;
            }
            case 'pickImage': {
                const inp = document.createElement('input');
                inp.type = 'file';
                inp.accept = 'image/*';
                inp.onchange = async () => {
                    const f = inp.files && inp.files[0];
                    if (!f) return;
                    const url = await uploadImage(entry.dotNetRef, f);
                    if (url) ed.chain().focus().setImage({ src: url }).run();
                };
                inp.click();
                break;
            }
            case 'pickWord': {
                const inp = document.createElement('input');
                inp.type = 'file';
                inp.accept = '.docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document';
                inp.onchange = async () => {
                    const f = inp.files && inp.files[0];
                    if (!f || !entry.dotNetRef) return;
                    try {
                        const buffer = await f.arrayBuffer();
                        // Convert ArrayBuffer → base64 in chunks to avoid call-stack overflow on large files.
                        const bytes = new Uint8Array(buffer);
                        let binary = '';
                        const chunk = 0x8000;
                        for (let i = 0; i < bytes.length; i += chunk) {
                            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
                        }
                        const base64 = btoa(binary);
                        await entry.dotNetRef.invokeMethodAsync('OnWordImportRequested', f.name, base64);
                    } catch (e) {
                        console.error('Word import failed:', e);
                    }
                };
                inp.click();
                break;
            }
            case 'link': {
                const url = args[0];
                if (!url) chain.extendMarkRange('link').unsetLink().run();
                else chain.extendMarkRange('link').setLink({ href: url }).run();
                break;
            }
            case 'unlink': chain.extendMarkRange('link').unsetLink().run(); break;
            case 'replaceSelection': {
                const html = args[0];
                if (typeof html === 'string') {
                    chain.deleteSelection().insertContent(html).run();
                }
                break;
            }
            case 'snapshot': {
                entry.snapshots.push({ at: Date.now(), html: ed.getHTML() });
                if (entry.snapshots.length > 50) entry.snapshots.shift();
                break;
            }
            case 'restoreSnapshot': {
                const i = typeof args[0] === 'number' ? args[0] : entry.snapshots.length - 1;
                const snap = entry.snapshots[i];
                if (snap) ed.commands.setContent(snap.html, true);
                break;
            }
            case 'undo': chain.undo().run(); break;
            case 'redo': chain.redo().run(); break;
            case 'clearFormat': chain.unsetAllMarks().clearNodes().run(); break;
        }
    },

    getActive(id) {
        const entry = instances.get(id);
        if (!entry) return null;
        const ed = entry.editor;
        return {
            bold: ed.isActive('bold'),
            italic: ed.isActive('italic'),
            underline: ed.isActive('underline'),
            strike: ed.isActive('strike'),
            code: ed.isActive('code'),
            paragraph: ed.isActive('paragraph'),
            heading1: ed.isActive('heading', { level: 1 }),
            heading2: ed.isActive('heading', { level: 2 }),
            heading3: ed.isActive('heading', { level: 3 }),
            bulletList: ed.isActive('bulletList'),
            orderedList: ed.isActive('orderedList'),
            blockquote: ed.isActive('blockquote'),
            codeBlock: ed.isActive('codeBlock'),
            link: ed.isActive('link'),
            canUndo: ed.can().undo(),
            canRedo: ed.can().redo(),
        };
    },

    getSnapshots(id) {
        const entry = instances.get(id);
        if (!entry) return [];
        return entry.snapshots.map((s, i) => ({ index: i, at: s.at }));
    },

    destroy(id) {
        const entry = instances.get(id);
        if (!entry) return;
        try { entry.bubble && entry.bubble.destroy(); } catch (_) {}
        try { entry.drag && entry.drag.destroy(); } catch (_) {}
        try { entry.editor.destroy(); } catch (_) {}
        instances.delete(id);
    },

    promptLink(initial) {
        const url = window.prompt('Enter URL', initial || 'https://');
        if (url === null) return null;
        const trimmed = url.trim();
        return trimmed.length === 0 ? '' : trimmed;
    },
};

async function uploadImage(dotNetRef, file) {
    try {
        if (dotNetRef) {
            const url = await dotNetRef.invokeMethodAsync('OnImageRequested', file.name, file.type, file.size);
            if (url) return url;
        }
    } catch (_) {}
    // Fallback: base64 inline.
    return new Promise(resolve => {
        const r = new FileReader();
        r.onload = () => resolve(String(r.result));
        r.onerror = () => resolve(null);
        r.readAsDataURL(file);
    });
}

function defaultSlashItems(query) {
    const all = [
        { id: 'h1', label: 'Heading 1', subtitle: 'Big section heading', icon: 'Heading1' },
        { id: 'h2', label: 'Heading 2', subtitle: 'Medium section heading', icon: 'Heading2' },
        { id: 'h3', label: 'Heading 3', subtitle: 'Small section heading', icon: 'Heading3' },
        { id: 'bullet', label: 'Bullet list', subtitle: 'Simple list', icon: 'List' },
        { id: 'ordered', label: 'Numbered list', subtitle: 'Ordered list', icon: 'ListOrdered' },
        { id: 'task', label: 'Task list', subtitle: 'Check items off', icon: 'ListChecks' },
        { id: 'quote', label: 'Quote', subtitle: 'Block quote', icon: 'Quote' },
        { id: 'code', label: 'Code block', subtitle: 'Syntax-highlighted code', icon: 'SquareCode' },
        { id: 'table', label: 'Table', subtitle: 'Insert a 3×3 table', icon: 'Table' },
        { id: 'image', label: 'Image', subtitle: 'Upload from your device', icon: 'Image' },
        { id: 'divider', label: 'Divider', subtitle: 'Horizontal rule', icon: 'Minus' },
    ];
    if (!query) return all;
    const q = query.toLowerCase();
    return all.filter(i => i.label.toLowerCase().includes(q) || i.id.includes(q));
}

// Expose on window for direct JS interop callers.
if (typeof window !== 'undefined') {
    window.lumeoRichTextEditor = rte;
}

export default rte;
