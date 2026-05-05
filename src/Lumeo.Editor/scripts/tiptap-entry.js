// Lumeo.Editor — single-file TipTap bundle entry point.
//
// esbuild bundles this file into wwwroot/js/tiptap-bundle.js as one ESM module.
// rich-text-editor.js loads the bundle once and pulls every TipTap/ProseMirror
// primitive plus the lowlight registry off the resulting module namespace —
// no esm.sh, no per-extension import waterfall, CSP-safe, works offline.
//
// Re-export shape matches what loadTiptap() in rich-text-editor.js destructures.
// When you add or remove an extension, update both this file and the consumer.

export * as core from "@tiptap/core";
export * as pmState from "@tiptap/pm/state";
export * as pmView from "@tiptap/pm/view";

export * as starterKit from "@tiptap/starter-kit";
export * as link from "@tiptap/extension-link";
export * as underline from "@tiptap/extension-underline";
export * as placeholder from "@tiptap/extension-placeholder";
export * as typography from "@tiptap/extension-typography";

export * as mention from "@tiptap/extension-mention";
export * as suggestion from "@tiptap/suggestion";

export * as table from "@tiptap/extension-table";
export * as tableRow from "@tiptap/extension-table-row";
export * as tableCell from "@tiptap/extension-table-cell";
export * as tableHeader from "@tiptap/extension-table-header";

export * as image from "@tiptap/extension-image";
export * as taskList from "@tiptap/extension-task-list";
export * as taskItem from "@tiptap/extension-task-item";

export * as codeBlockLowlight from "@tiptap/extension-code-block-lowlight";
export * as lowlightMod from "lowlight";

// highlight.js languages — kept individually so the editor can call
// lowlight.register(name, lang) for the small set we want syntax-highlighted.
// The lazy `.catch(() => null)` from the old esm.sh path isn't needed here
// because esbuild resolves these at build time.
export { default as jsHl } from "highlight.js/lib/languages/javascript";
export { default as tsHl } from "highlight.js/lib/languages/typescript";
export { default as pyHl } from "highlight.js/lib/languages/python";
export { default as cssHl } from "highlight.js/lib/languages/css";
export { default as htmlHl } from "highlight.js/lib/languages/xml";
export { default as jsonHl } from "highlight.js/lib/languages/json";
export { default as bashHl } from "highlight.js/lib/languages/bash";
