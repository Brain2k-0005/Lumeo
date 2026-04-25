// Lumeo RichTextEditor — thin wrapper around TipTap v2.
// TipTap is loaded lazily from esm.sh the first time the editor is used.
// The promise is cached so subsequent editors reuse the same import.

let _tiptapPromise = null;

async function loadTiptap() {
    if (_tiptapPromise) return _tiptapPromise;
    _tiptapPromise = (async () => {
        const [core, starterKit, link, underline, placeholder] = await Promise.all([
            import('https://esm.sh/@tiptap/core@2'),
            import('https://esm.sh/@tiptap/starter-kit@2'),
            import('https://esm.sh/@tiptap/extension-link@2'),
            import('https://esm.sh/@tiptap/extension-underline@2'),
            import('https://esm.sh/@tiptap/extension-placeholder@2'),
        ]);
        return {
            Editor: core.Editor,
            StarterKit: starterKit.default || starterKit.StarterKit,
            Link: link.default || link.Link,
            Underline: underline.default || underline.Underline,
            Placeholder: placeholder.default || placeholder.Placeholder,
        };
    })();
    return _tiptapPromise;
}

const instances = new Map();

function makeId() {
    return 'rte-' + Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}

export const rte = {
    async init(el, dotNetRef, options) {
        const opts = options || {};
        const { Editor, StarterKit, Link, Underline, Placeholder } = await loadTiptap();

        const editor = new Editor({
            element: el,
            extensions: [
                StarterKit,
                Underline,
                Link.configure({
                    openOnClick: false,
                    autolink: true,
                    HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' },
                }),
                Placeholder.configure({ placeholder: opts.placeholder || 'Start writing…' }),
            ],
            content: opts.content || '',
            editable: !(opts.disabled || opts.readOnly),
            editorProps: {
                attributes: {
                    class: 'prose prose-sm max-w-none p-4 focus:outline-none',
                },
            },
            onUpdate: ({ editor: ed }) => {
                const html = ed.getHTML();
                try {
                    dotNetRef.invokeMethodAsync('OnContentUpdate', html);
                } catch (_) {
                    // circuit may be gone; swallow
                }
            },
            onSelectionUpdate: () => {
                try {
                    dotNetRef.invokeMethodAsync('OnSelectionUpdate');
                } catch (_) { /* noop */ }
            },
        });

        const id = makeId();
        instances.set(id, { editor, dotNetRef });
        return id;
    },

    setContent(id, html) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.commands.setContent(html ?? '', false);
    },

    command(id, name, ...args) {
        const entry = instances.get(id);
        if (!entry) return;
        const chain = entry.editor.chain().focus();
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
            case 'blockquote': chain.toggleBlockquote().run(); break;
            case 'codeBlock': chain.toggleCodeBlock().run(); break;
            case 'hr': chain.setHorizontalRule().run(); break;
            case 'link': {
                const url = args[0];
                if (!url) {
                    chain.extendMarkRange('link').unsetLink().run();
                } else {
                    chain.extendMarkRange('link').setLink({ href: url }).run();
                }
                break;
            }
            case 'unlink': chain.extendMarkRange('link').unsetLink().run(); break;
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

    setDisabled(id, disabled) {
        const entry = instances.get(id);
        if (!entry) return;
        entry.editor.setEditable(!disabled);
    },

    getHtml(id) {
        const entry = instances.get(id);
        if (!entry) return '';
        return entry.editor.getHTML();
    },

    destroy(id) {
        const entry = instances.get(id);
        if (!entry) return;
        try { entry.editor.destroy(); } catch (_) { /* noop */ }
        instances.delete(id);
    },

    // Small helper for the Blazor component — uses browser prompt() to collect a URL.
    promptLink(initial) {
        const url = window.prompt('Enter URL', initial || 'https://');
        if (url === null) return null;
        const trimmed = url.trim();
        return trimmed.length === 0 ? '' : trimmed;
    },
};

export default rte;
