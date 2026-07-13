#!/usr/bin/env node
// scripts/sr-audit/run.mjs
//
// Guidepup-driven NVDA automation of docs/superpowers/sr-test-protocol.md.
//
// Two modes:
//   1. COMPONENTS sweep (default) — the protocol's full top-20 list, each with a
//      hand-verified `focusSelector` (evidence-based, see comments per entry) and a
//      subset of the protocol's numbered rows exercised as focus + key-press steps,
//      scored PASS/FAIL/LOGGED against conservative expected keywords.
//   2. --all generic sweep — every OTHER component page the docs registry knows about
//      (164 total minus cdn-deps, minus the 20 above) gets a lighter-weight probe:
//      focus the first `[tabindex="0"]` in its demo area, press ArrowDown/ArrowRight,
//      and classify the page as "no-focusable" / "silent" / "spoke". This is a coarse
//      keyboard-reachability smoke test, not a full protocol walkthrough — it exists to
//      surface components that silently have NO keyboard/SR surface at all, which is
//      exactly the kind of regression a per-component protocol entry would miss until
//      someone thinks to add it.
//
// A few protocol components have NO focusable/keyboard surface in their current docs
// demo at all (confirmed empirically, not guessed) — see the SKIPPED array below
// instead of a fabricated COMPONENTS entry for them.
//
// IMPORTANT — run `node check-env.mjs` first. On a shared/locked-down
// interactive desktop this script's speech capture can silently return
// empty strings for a reason that has nothing to do with Guidepup or NVDA:
// see README.md "Known blocker: OS foreground focus". check-env.mjs
// diagnoses that specific failure mode before you spend time on a full run.
//
// Usage:
//   node run.mjs                  # build + run full sweep against docs/Lumeo.Docs
//   node run.mjs --no-build       # docs site already built (dotnet build -c Release)
//   node run.mjs --base-url <url> # crawl an already-running docs server
//   node run.mjs --component tabs # single component, for fast iteration
//   node run.mjs --all            # ALSO generic-probe every non-top-20 component page
//
// Writes results/nvda-<date>.json (PASS/PARTIAL/FAIL per step, actual vs
// expected spoken text, plus a `generic` section when --all was used) and
// prints the same summary to stdout.

import { nvda } from "@guidepup/guidepup";
import { chromium } from "playwright-core";
import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";
import { join, resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as sleep } from "node:timers/promises";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "..", "..");
const resultsDir = join(__dirname, "results");

const args = process.argv.slice(2);
const noBuild = args.includes("--no-build");
const baseUrlArg = args.includes("--base-url") ? args[args.indexOf("--base-url") + 1] : null;
const componentArg = args.includes("--component") ? args[args.indexOf("--component") + 1] : null;
// --all: after the COMPONENTS sweep, also generic-probe every OTHER component page the
// docs registry knows about (see runGenericSweep below). Off by default because it adds
// ~25-30 minutes to the run (164 pages) — opt in explicitly.
const allMode = args.includes("--all");
// --pause: after Edge launches, give the operator a fixed window to CLICK the Edge
// window (a direct click is the one thing that always grants OS foreground focus),
// then auto-start the sweep on a countdown. Deliberately does NOT wait for a terminal
// keypress — pressing Enter in the terminal would pull foreground focus straight back
// off Edge, re-triggering the exact "NVDA reads the wrong window" blocker (README).
const pauseForForeground = args.includes("--pause");
// --force-foreground: programmatically drag the Edge window to real OS foreground focus
// (via force-foreground.ps1) instead of asking a human to click it — enables a fully
// unattended run (e.g. scheduled overnight). Re-applied before every component in case
// focus drifts back to another window mid-sweep.
const forceForeground = args.includes("--force-foreground");
const PORT = process.env.SR_AUDIT_PORT || 5292;

// Spawn the PowerShell foreground helper synchronously; never throws. Returns true only
// when the helper confirmed the Edge window actually became foreground — callers must gate
// any physical input on this (Codex P2, PR #368: clicking while a security dialog or another
// app still owns the desktop would send a real click into THAT window).
function forceEdgeForeground() {
    if (!forceForeground) return false;
    try {
        const ps = join(__dirname, "force-foreground.ps1");
        const r = spawnSync("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps, "-Match", "Lumeo"], { encoding: "utf8" });
        if (r.stdout) process.stdout.write(`[sr-audit] ${r.stdout.trim()}\n`);
        return r.status === 0;
    } catch (e) { console.warn(`[sr-audit] force-foreground failed: ${e.message}`); return false; }
}

// After the window is CONFIRMED foreground, physically click the page HEADING (h1) to move
// OS keyboard focus INTO the page content. el.focus() alone only sets DOM focus, so without
// this NVDA keeps reading the browser toolbar. The heading is a neutral, per-page target
// (no side effects; its Y differs per page, which is exactly why a fixed coordinate was
// fragile — DataGrid's heading sits higher than Tabs'). Passes CSS viewport coords +
// innerHeight + dpr; click-at.ps1 anchors them to the OS window rect, deliberately avoiding
// window.screenX/screenY whose meaning is ambiguous across Chromium versions (Codex P2).
// Best-effort; never throws.
async function clickHeadingIntoContent(page) {
    if (!forceForeground) return;
    try {
        const c = await page.evaluate(() => {
            const h = document.querySelector('main h1, h1');
            if (!h) return null;
            const r = h.getBoundingClientRect();
            if (r.width === 0 && r.height === 0) return null;
            return {
                cssX: r.left + Math.min(r.width / 2, 80),
                cssY: r.top + r.height / 2,
                innerH: window.innerHeight,
                dpr: window.devicePixelRatio || 1,
            };
        });
        if (!c) return;
        const r = spawnSync("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", join(__dirname, "click-at.ps1"),
            "-Match", "Lumeo", "-CssX", String(c.cssX), "-CssY", String(c.cssY), "-InnerH", String(c.innerH), "-Dpr", String(c.dpr)], { encoding: "utf8" });
        if (r.stdout && !/OK/.test(r.stdout)) process.stdout.write(`[sr-audit] ${r.stdout.trim()}\n`);
    } catch (e) { console.warn(`[sr-audit] heading-click failed: ${e.message}`); }
}

// ---------------------------------------------------------------------------
// Protocol subset: top-5 components by keyboard-interaction depth, from
// docs/superpowers/sr-test-protocol.md. Each step maps 1:1 to a numbered row
// in that file's table for the component. `focusSelector` substitutes for
// "Tab to <the widget>" — instead of blindly Tab-ing through the docs page's
// topbar/sidebar chrome (fragile, changes with every nav edit), it focuses
// the first element matching the widget's ARIA role directly inside <main>.
// This exercises the real widget's real keyboard handling faithfully; it
// just doesn't also re-verify the page's outer Tab order, which is already
// covered by the repo's keyboard-interaction test suite, not this protocol.
// ---------------------------------------------------------------------------
const COMPONENTS = [
    {
        name: "DataGrid",
        slug: "data-grid",
        // NOT the `<table role="grid">` container — it carries no tabindex at
        // all, so it is not script-focusable and `.focus()` on it silently
        // no-ops (the WAI-ARIA grid pattern's roving tabindex lives on the
        // header/body CELLS, exactly one of which carries tabindex="0" at any
        // time; see DataGridCell.razor/DataGridHeaderCell.razor TabIndexValue).
        focusSelector: '[role="grid"] [role="gridcell"][tabindex="0"], [role="grid"] [role="columnheader"][tabindex="0"], ' +
            '[role="treegrid"] [role="gridcell"][tabindex="0"], [role="treegrid"] [role="columnheader"][tabindex="0"]',
        steps: [
            // "table"/"grid" are true alternatives here (some SR/browser combos
            // announce the grid role as "table", others as "grid") — either one
            // proves the role was spoken, so this stays an OR check.
            { row: 1, action: "focus", key: null, expected: ["column header", "grid", "table"] },
            { row: 2, action: "press", key: "ArrowRight", expected: [] }, // no fixed text; logged for manual eyeball
            { row: 3, action: "press", key: "ArrowDown", expected: [] },
        ],
    },
    {
        name: "FileManager",
        slug: "file-manager",
        // The `[role="tree"]` container carries no tabindex (same as DataGrid's grid) — the
        // roving-tabindex keyboard target is the active `role="treeitem"` (the folder tree got
        // full WAI-ARIA keyboard nav in the FileManager-tree-keyboard fix; before that the tree
        // was mouse-only, which this audit is what surfaced). Focus the active treeitem.
        focusSelector: '[role="treeitem"] [tabindex="0"], [role="tree"] [tabindex="0"], [role="treeitem"][tabindex="0"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["tree"] },
            { row: 2, action: "press", key: "ArrowDown", expected: [] },
            { row: 3, action: "press", key: "ArrowRight", expected: ["expanded"] },
        ],
    },
    {
        name: "Tabs",
        slug: "tabs",
        focusSelector: '[role="tablist"] [role="tab"]',
        steps: [
            // Conjunctive, not alternatives: the row is only a real PASS if BOTH
            // the "tab" role AND the "selected" state are spoken — an SR that
            // announces the generic role but drops the selected state (exactly
            // the class of regression this audit exists to catch) must FAIL,
            // not silently pass because "tab" alone satisfied an OR check.
            { row: 1, action: "focus", key: null, expected: ["tab", "selected"], matchMode: "all" },
            { row: 2, action: "press", key: "ArrowRight", expected: [] },
            { row: 3, action: "press", key: "Home", expected: [] },
        ],
    },
    {
        name: "Calendar",
        slug: "calendar",
        // The `[role="gridcell"]` itself is a plain, tabindex-less <div> (pure
        // ARIA structure) — the actual roving-tabindex keyboard target is the
        // native <button> it wraps (see Calendar.razor DayTabIndex). Focusing
        // the gridcell div would silently no-op like the DataGrid case above.
        focusSelector: '[role="grid"] button[tabindex="0"]',
        steps: [
            // Alternatives (same reasoning as DataGrid row 1 above): "grid" and
            // "table" are two vocabularies different SR/browser combos use for
            // the same role, not two independent facts that both must be true.
            { row: 1, action: "focus", key: null, expected: ["button", "grid", "table"] },
            { row: 2, action: "press", key: "ArrowRight", expected: [] },
            { row: 4, action: "press", key: "PageDown", expected: [] },
        ],
    },
    {
        name: "Cascader",
        slug: "cascader",
        // Cascader's trigger button has neither `aria-haspopup` nor
        // `role="combobox"` (see Cascader.razor) — `aria-haspopup="menu"` only
        // shows up on the nested option <button>s, which don't exist in the
        // DOM until the popup is opened, so this selector matched nothing on
        // page load. The trigger is the wrapper's own first direct-child
        // <button> (id is deterministically prefixed "cascader-" via
        // LumeoIds.New); the clear/chevron controls are nested INSIDE that
        // same button, not siblings, so `> button` can't accidentally match
        // them instead.
        focusSelector: '[id^="cascader-"] > button',
        steps: [
            // Conjunctive, not alternatives (same reasoning as Tabs row 1 above):
            // the trigger must be spoken as BOTH a button AND a combobox — an SR
            // that only announces the generic "button" and drops the combobox
            // semantics is a real regression, not a PASS.
            { row: 1, action: "focus", key: null, expected: ["button", "menu"], matchMode: "all" }, // post aria-haspopup fix: NVDA announces the menu popup on the trigger
            { row: 2, action: "press", key: "Enter", expected: ["menu"] },
            { row: 7, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "ColorPicker",
        slug: "color-picker",
        // Empirically (headless playwright-core dump of /components/color-picker's demo
        // area): the swatch trigger is a plain native <button aria-haspopup="dialog"
        // aria-expanded="false" aria-label="Pick a color">#3B82F6</button> — no explicit
        // tabindex needed (buttons are natively focusable), and the aria-label already
        // carries the current color per protocol row 1.
        focusSelector: 'button[aria-haspopup="dialog"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["button"] },
            { row: 2, action: "press", key: "Enter", expected: ["dialog"] },
            { row: 7, action: "press", key: "Escape", expected: [] }, // dialog closes; new color text is unpredictable, logged
        ],
    },
    {
        name: "Combobox",
        slug: "combobox",
        // Evidence: <input role="combobox" aria-haspopup="listbox" aria-expanded="false">
        // — a native <input>, natively focusable without an explicit tabindex attribute.
        focusSelector: 'input[role="combobox"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["combo"] },
            { row: 3, action: "press", key: "ArrowDown", expected: [] }, // filtered option text unpredictable, logged
            { row: 6, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "SpeedDial",
        slug: "speed-dial",
        // Evidence: <button id="speeddial-trigger-…" aria-haspopup="menu"
        // aria-expanded="false" aria-label="Actions"> — native button, no tabindex needed.
        focusSelector: '[id^="speeddial-trigger-"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["button"] },
            { row: 2, action: "press", key: "Enter", expected: ["menu", "expanded"] }, // NVDA announces the open state as "expanded" on the trigger
            { row: 6, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "TreeSelect",
        slug: "tree-select",
        // Evidence: <button id="treeselect-trigger-…" aria-haspopup="tree">Select…</button>
        // — native button trigger; the nested "Clear selection" span[role=button] is a
        // secondary control, not the main trigger, so it's deliberately not targeted here.
        focusSelector: 'button[aria-haspopup="tree"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["button"] },
            { row: 2, action: "press", key: "Enter", expected: ["tree"] },
            { row: 6, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "Select",
        slug: "select",
        // Evidence: <button role="combobox" id="select-trigger-…" aria-haspopup="listbox"
        // aria-expanded="false">Select an option</button> — native button, no tabindex
        // attribute present or needed.
        focusSelector: 'button[role="combobox"][id^="select-trigger-"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["combo"] },
            { row: 2, action: "press", key: "Enter", expected: ["list"] }, // NVDA speaks the listbox role as "list"
            { row: 6, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "Splitter",
        slug: "splitter",
        // Evidence: every <div role="separator" id="splitter-divider-…" tabindex="0">
        // found in the demo — unlike DataGrid/Calendar this is NOT a roving-tabindex
        // pattern; each divider is independently tabbable, so the first DOM match is a
        // legitimate, real target (not an arbitrary pick among many equal candidates).
        focusSelector: '[role="separator"][tabindex="0"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["separator"] },
            { row: 2, action: "press", key: "ArrowRight", expected: [] }, // value-change text unpredictable, logged
            { row: 3, action: "press", key: "Home", expected: [] },
        ],
    },
    {
        name: "TreeView",
        slug: "tree-view",
        // Evidence: every <div role="treeitem" id="tree-…-node-0" tabindex="0"
        // aria-expanded="true|false"> found in the demo — same "every item independently
        // tabbable" shape as Splitter above, not a single roving-tabindex node.
        focusSelector: '[role="treeitem"][tabindex="0"]',
        steps: [
            // "tree" is a safe substring match for either "tree" or "treeitem"/"tree item"
            // being spoken — this is the initial-focus row, so either vocabulary proves
            // the role was announced.
            { row: 1, action: "focus", key: null, expected: ["tree"] },
            { row: 3, action: "press", key: "ArrowRight", expected: ["expanded", "level"] }, // descend announces the child ("level N, ...") — equally correct
            { row: 6, action: "press", key: "Home", expected: [] },
        ],
    },
    {
        name: "DropdownMenu",
        slug: "dropdown-menu",
        // NOT the outer `<div id="dropdown-trigger-…" role="button" aria-haspopup="menu"
        // aria-expanded="false">` — confirmed via a targeted focus() + activeElement check
        // that this div has tabIndex === -1 (not script-focusable; el.focus() moved focus
        // to <h1> instead, exactly the "container has no tabindex" mistake this task warns
        // about). The div wraps one plain native `<button type="button">` with no ARIA of
        // its own but tabIndex === 0 — THAT nested button is the real keyboard target.
        focusSelector: '[id^="dropdown-trigger-"] > button',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["button"] },
            { row: 2, action: "press", key: "Enter", expected: ["menu"] },
            { row: 7, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "MegaMenu",
        slug: "mega-menu",
        // Evidence: <button role="menuitem" id="megamenu-trigger-…" aria-haspopup="true"
        // aria-expanded="false" tabindex="0">Products</button> — a real native button,
        // unlike DropdownMenu above; only the FIRST top-level trigger in each menubar
        // carries tabindex="0" (roving tabindex across the row), later ones are -1, so
        // this selector already lands on a real, currently-reachable trigger.
        focusSelector: 'button[role="menuitem"][aria-haspopup="true"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["menu"] }, // role=menuitem wins over the native button: NVDA says "menu item"
            { row: 2, action: "press", key: "Enter", expected: [] }, // panel/column content unpredictable, logged
            { row: 5, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "Mention",
        slug: "mention",
        // Evidence: <textarea role="combobox" id="mention-…" aria-haspopup="listbox"
        // aria-expanded="false"> — native textarea, no tabindex needed.
        focusSelector: 'textarea[role="combobox"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["combo"] },
            // Protocol row 1's second half ("...type @") / row 2 ("type a filter fragment")
            // collapsed into one step here: the step model presses one key against the
            // already-focused element, so "type @" is the only single keypress that
            // actually starts the mention flow (opens the popup) — logged, not asserted,
            // since Guidepup's press("@") support for a bare symbol key isn't guaranteed
            // across SR/browser combos.
            { row: 2, action: "press", key: "@", expected: [] },
            { row: 5, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "Menubar",
        slug: "menubar",
        // Evidence: <button role="menuitem" id="menubar-menu-…-trigger" aria-haspopup="menu"
        // aria-expanded="false" tabindex="0"> — native button; only the first top-level
        // item carries tabindex="0" (roving tabindex), rest are -1.
        focusSelector: '[id*="menubar-menu-"][role="menuitem"]',
        steps: [
            // "menu" is a deliberately loose substring match: NVDA may render "menuitem" as
            // "menu item" (with a space), which a literal "menuitem" keyword would miss.
            { row: 1, action: "focus", key: null, expected: ["menu"] },
            { row: 3, action: "press", key: "Enter", expected: ["menu"] },
            { row: 5, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "NavigationMenu",
        slug: "navigation-menu",
        // Evidence: <button id="nav-trigger-…" aria-haspopup="menu" aria-expanded="false"
        // tabindex="0">Getting Started</button> — native button, no roving-tabindex
        // ambiguity (this one carries tabindex="0" directly, unlike MegaMenu/Menubar's
        // first-of-row pattern, but the evidence check confirms it either way).
        focusSelector: 'button[aria-haspopup="menu"][id^="nav-trigger-"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["menu"] },
            { row: 2, action: "press", key: "Enter", expected: [] }, // first panel item unpredictable, logged
            { row: 5, action: "press", key: "Escape", expected: [] },
        ],
    },
    {
        name: "OtpInput",
        slug: "otp-input",
        // Evidence: <input id="otp-…-0" aria-label="Digit 1 of 6"> — native input, no
        // tabindex needed; "digit" is a safe keyword since EVERY demo's box label follows
        // the same "Digit N of M" pattern regardless of which demo this selector's first
        // DOM match lands in.
        focusSelector: 'input[id^="otp-"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["digit"] },
            { row: 3, action: "press", key: "ArrowRight", expected: [] },
            { row: 5, action: "press", key: "Home", expected: [] },
        ],
    },
];

// ---------------------------------------------------------------------------
// Protocol components with NO empirically-confirmed keyboard/SR surface in
// their current docs demo — real audit signal, not a gap in this script.
// Each entry's `reason` was verified with a headless playwright-core probe
// (focus() + document.activeElement check), not guessed.
// ---------------------------------------------------------------------------
const SKIPPED = [
    {
        name: "ContextMenu",
        slug: "context-menu",
        reason: 'Demo trigger area is `<div id="ctx-trigger-…" aria-expanded="false">' +
            '<div>Right click here</div></div>` — neither div carries a `role` or a ' +
            '`tabindex`. Confirmed via el.focus(): tabIndex === -1 and the call is a ' +
            "no-op (document.activeElement stayed on <h1>). The protocol's row 1 " +
            '("Focus the trigger area, open via Shift+F10") has no way to reach step 1 ' +
            "with a keyboard in this demo — it is mouse/right-click only as currently " +
            "rendered, not merely untested.",
    },
    {
        name: "Kanban",
        slug: "kanban",
        reason: "Every demo card is `<div draggable=\"true\" class=\"bg-card…\">` with " +
            "no `role`, no `tabindex`, and no `aria-roledescription` (confirmed via a " +
            "headless DOM dump of all `[draggable]` nodes in the demo — 10 found, all " +
            "identical shape). The protocol's whole row set (grab/move/drop via keyboard) " +
            "assumes a focusable, ARIA-described draggable card; this demo implements " +
            "pointer-only HTML5 drag-and-drop with no keyboard equivalent at all.",
    },
];

if (componentArg) {
    const filtered = COMPONENTS.filter((c) => c.slug === componentArg || c.name.toLowerCase() === componentArg.toLowerCase());
    if (filtered.length === 0) {
        console.error(`No component matches --component ${componentArg}. Known: ${COMPONENTS.map((c) => c.slug).join(", ")}`);
        process.exit(1);
    }
    COMPONENTS.length = 0;
    COMPONENTS.push(...filtered);
}

// mode "any" (default): expectedKeywords are alternatives — different SR/
// browser vocabularies for the same fact — so one match is a PASS.
// mode "all": expectedKeywords are conjunctive facts (e.g. role + state) that
// must ALL be spoken; matching only one is exactly the kind of partial/
// regressed announcement this audit exists to catch, so it must FAIL.
function fuzzyContains(actual, expectedKeywords, mode = "any") {
    if (expectedKeywords.length === 0) return null; // no assertion, just logged
    const a = (actual || "").toLowerCase();
    return mode === "all"
        ? expectedKeywords.every((kw) => a.includes(kw.toLowerCase()))
        : expectedKeywords.some((kw) => a.includes(kw.toLowerCase()));
}

function run(cmd, cmdArgs, opts = {}) {
    return new Promise((resolveP, rejectP) => {
        const p = spawn(cmd, cmdArgs, { stdio: "inherit", shell: process.platform === "win32", ...opts });
        p.on("exit", (code) => (code === 0 ? resolveP() : rejectP(new Error(`${cmd} ${cmdArgs.join(" ")} exited ${code}`))));
        p.on("error", rejectP);
    });
}

async function waitForServer(url, timeoutMs = 120_000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        try {
            const res = await fetch(url);
            if (res.ok || res.status === 404) return true;
        } catch { /* not up yet */ }
        await sleep(1000);
    }
    return false;
}

// Navigates to a component page, waits for Blazor readiness, and re-asserts Edge OS
// foreground focus (+ the physical heading click that moves keyboard focus INTO page
// content) exactly as the original COMPONENTS loop did inline. Shared by both the
// detailed COMPONENTS sweep and the generic --all sweep so the two don't duplicate this
// navigation/foreground dance. `blazorTimeoutMs`/`settleMs` are shorter for the generic
// sweep (bounded per-page time across ~150 pages) than for the detailed 20-component walk.
async function gotoComponentPage(page, baseUrl, slug, { blazorTimeoutMs = 30_000, settleMs = 1000 } = {}) {
    const route = `/components/${slug}`;
    await page.goto(baseUrl + route, { waitUntil: "load", timeout: 60_000 });
    try {
        await page.waitForFunction(() => document.documentElement.dataset.blazorReady === "true", { timeout: blazorTimeoutMs });
    } catch {
        console.warn(`[sr-audit] ${slug}: blazorReady never set within ${blazorTimeoutMs}ms — continuing anyway`);
    }
    await sleep(settleMs);
    // Re-assert Edge foreground before each page: NVDA only reads the foreground window,
    // and a mid-sweep navigation or dialog can steal it. Only follow up with the physical
    // heading click when Edge is CONFIRMED foreground — otherwise the click would land in
    // whatever window actually owns the desktop (Codex P2). The heading click is what
    // moves OS keyboard focus into page content (the missing piece for heavier layouts).
    if (forceEdgeForeground()) {
        await clickHeadingIntoContent(page);
        await sleep(250);
    } else if (forceForeground) {
        console.warn(`[sr-audit] ${slug}: Edge did not reach foreground — skipping content click; speech capture may read the wrong window`);
    }
    return route;
}

// Resolves the full set of component slugs the docs registry knows about, for --all.
// Prefers the LIVE registry.json served by the already-running docs server (works
// against any --base-url, and reflects exactly what that server currently serves);
// falls back to reading the registry JSON files off disk if the route isn't reachable.
async function getAllSlugs(baseUrl) {
    try {
        const res = await fetch(`${baseUrl}/registry.json`);
        if (res.ok) {
            const data = await res.json();
            const slugs = Object.keys(data.components || {}).filter((s) => s !== "cdn-deps");
            if (slugs.length > 0) return slugs.sort();
        }
    } catch { /* fall through to filesystem */ }
    const registryDir = join(repoRoot, "docs", "Lumeo.Docs", "wwwroot", "registry");
    const files = readdirSync(registryDir).filter((f) => f.endsWith(".json") && f !== "cdn-deps.json");
    return files.map((f) => f.replace(/\.json$/, "")).sort();
}

// Generic keyboard-reachability probe for --all, used on every component page NOT
// already covered by a detailed COMPONENTS entry above. Deliberately lightweight (one
// focus + two key presses) — its job is coarse classification (is this page keyboard-
// reachable at all, and does NVDA say anything on basic arrow navigation), not a full
// protocol walkthrough.
async function runGenericProbe(page, slug) {
    const focusInfo = await page.evaluate(() => {
        // Scope to the demo area the same way discover-selectors did during development
        // (excludes topbar/sidebar/docs chrome such as the sidebar nav and TOC), falling
        // back to <main> if no dedicated preview wrapper is found.
        const main = document.querySelector("main");
        if (!main) return { found: false };
        const host = main.querySelector('[class*="preview"]') || main;
        const el = host.querySelector('[tabindex="0"]');
        if (!el) return { found: false };
        el.focus();
        return {
            found: true,
            focused: document.activeElement === el,
            tag: el.tagName.toLowerCase(),
            role: el.getAttribute("role"),
        };
    });
    if (!focusInfo.found || !focusInfo.focused) {
        return { focusable: focusInfo.found, focusOk: !!focusInfo.focused, announcements: [], classification: "no-focusable" };
    }
    const announcements = [];
    // First: an explicit focus REPORT — the one announcement every focusable component
    // must produce (its accessible name/role/state as NVDA speaks it). This is the
    // per-page "tested with NVDA" proof; the arrow presses below are extra navigation
    // signal that may legitimately be silent (a plain button ignores arrows).
    {
        await nvda.clearSpokenPhraseLog();
        try { await nvda.perform(nvda.keyboardCommands.reportCurrentFocus); } catch { /* best-effort */ }
        await sleep(600);
        let actual = await nvda.lastSpokenPhrase();
        if (actual && actual === runGenericProbe._lastCross) actual = "";
        announcements.push({ key: "(reportCurrentFocus)", actual });
        if (actual) runGenericProbe._lastCross = actual;
    }
    for (const key of ["ArrowDown", "ArrowRight"]) {
        await nvda.clearSpokenPhraseLog();
        await nvda.press(key);
        await sleep(500);
        let actual = await nvda.lastSpokenPhrase();
        // STALE-ECHO GUARD: when the pressed key changes nothing on the page, NVDA can
        // re-surface its previous utterance instead of staying silent — the first full
        // --all run reported the SAME phrase (the detailed sweep's last Tabs announcement)
        // on ~145 consecutive pages, which is physically impossible as real output.
        // Treat an announcement identical to the last one captured on a PREVIOUS page as
        // an echo, not fresh speech.
        if (actual && actual === runGenericProbe._lastCross) actual = "";
        announcements.push({ key, actual });
        if (actual) runGenericProbe._lastCross = actual;
    }
    const spoke = announcements.some((a) => a.actual && a.actual.trim().length > 0);
    return {
        focusable: true,
        focusOk: true,
        focusedTag: focusInfo.tag,
        focusedRole: focusInfo.role,
        announcements,
        // NOTE: the speech dimension of this coarse probe is best-effort (see echo guard
        // above); the load-bearing classification is DOM focusability ("no-focusable" is
        // hard evidence of a keyboard-unreachable demo). "silent" therefore means "no
        // FRESH speech attributable to this page", not proof of an SR defect.
        classification: spoke ? "spoke" : "silent",
    };
}

async function main() {
    // ---- 1. docs server -----------------------------------------------------
    let dotnetProc = null;
    let baseUrl = baseUrlArg;
    if (!baseUrl) {
        const dotnetExe = process.env.DOTNET_EXE || "dotnet";
        const docsProj = join(repoRoot, "docs", "Lumeo.Docs", "Lumeo.Docs.csproj");
        if (!noBuild) {
            // Same Tailwind steps a11y-audit/run.mjs and .github/workflows/
            // a11y-audit.yml run before their own `dotnet build` — plain
            // `dotnet build` does NOT regenerate the committed CSS bundles
            // docs/Lumeo.Docs serves (no MSBuild hook for it). Without this,
            // an SR run after a Tailwind/source style change would exercise
            // stale visibility/layout/focus styling instead of the source
            // under test, producing misleading PASS/FAIL results.
            console.log("[sr-audit] npm run build:css (root utilities)...");
            await run("npm", ["run", "build:css"], { cwd: repoRoot });
            console.log("[sr-audit] npm run css:build (docs/Lumeo.Docs)...");
            await run("npm", ["run", "css:build"], { cwd: join(repoRoot, "docs", "Lumeo.Docs") });

            console.log("[sr-audit] dotnet build docs/Lumeo.Docs (Release)...");
            await run(dotnetExe, ["build", docsProj, "-c", "Release", "--nologo"]);
        }
        baseUrl = `http://localhost:${PORT}`;
        console.log(`[sr-audit] dotnet run docs/Lumeo.Docs --urls ${baseUrl} ...`);
        dotnetProc = spawn(dotnetExe, [
            "run", "--project", docsProj, "-c", "Release", "--no-launch-profile", "--no-build",
            "--urls", baseUrl,
        ], { cwd: repoRoot, shell: process.platform === "win32", stdio: ["ignore", "pipe", "pipe"] });
        // Both pipes must be drained — ASP.NET Core logs a request line per page
        // load during the crawl, and an unconsumed stdout pipe backpressures the
        // dotnet process once the OS pipe buffer fills, hanging the run in a way
        // that looks like a server timeout rather than a pipe issue.
        dotnetProc.stdout.on("data", (d) => process.stdout.write(`[docs-server] ${d}`));
        dotnetProc.stderr.on("data", (d) => process.stderr.write(`[docs-server] ${d}`));
        const up = await waitForServer(baseUrl, 120_000);
        if (!up) {
            console.error(`[sr-audit] docs server did not come up within 120s at ${baseUrl}`);
            dotnetProc.kill();
            // dotnet run typically spawns a child host process on Windows that
            // plain .kill() may not terminate — apply the same tree-kill fallback
            // the finally block below uses, so this early-exit path can't leave
            // an orphaned server process behind.
            if (process.platform === "win32" && dotnetProc.pid) {
                spawn("taskkill", ["/pid", String(dotnetProc.pid), "/T", "/F"], { stdio: "ignore", shell: true });
            }
            process.exit(1);
        }
        console.log(`[sr-audit] docs server ready at ${baseUrl}`);
        await sleep(2000);
    }

    // ---- 2. browser + NVDA ---------------------------------------------------
    // From here on, a headed browser, NVDA, and (usually) the docs server are
    // all running on the maintainer's Windows desktop. If any awaited step
    // below throws (page.goto timeout, a Guidepup press failing, etc.), the
    // whole block still needs to tear these back down — otherwise a failed
    // run leaves a visible browser window + NVDA + localhost server behind.
    // `browser`/`nvdaStarted` are declared here (not just inside the try) so
    // the `finally` can see how far setup actually got.
    let browser = null;
    let nvdaStarted = false;
    try {
        // NVDA must be started BEFORE the browser is launched, matching
        // check-env.mjs's order: NVDA reads whatever currently holds OS
        // foreground focus, and in environments where nvda.start() briefly
        // takes real foreground focus itself, launching the browser first
        // and starting NVDA after can invalidate a session that otherwise
        // passed check-env.mjs — later DOM-focus changes don't fix stale/
        // wrong-window OS foreground focus, only re-verified startup order does.
        console.log("[sr-audit] starting NVDA via Guidepup...");
        await nvda.start();
        nvdaStarted = true;
        await sleep(2000);

        const edgeCandidates = [
            "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
            "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
        ];
        const edgePath = edgeCandidates.find((p) => existsSync(p));
        // Protocol recommends Firefox as primary; Edge/Chrome are the documented
        // fallback (see sr-test-protocol.md Setup section) and are what's
        // reliably present without an extra playwright browser download.
        browser = await chromium.launch({ headless: false, executablePath: edgePath, args: ["--start-maximized"] });
        const page = await browser.newPage();

        if (forceForeground) {
            // Unattended path: drag Edge to foreground programmatically. A short settle
            // after launch first, so the window actually exists to be found.
            await sleep(1500);
            forceEdgeForeground();
            await sleep(800);
        } else if (pauseForForeground) {
            // The programmatic Edge launch above happens ~15s after the operator's own
            // keystroke (docs build/serve + nvda.start() all run first), long past Windows'
            // recent-input foreground-grant window — so Edge often opens WITHOUT real OS
            // foreground focus and NVDA would keep reading whatever does have it. A direct
            // mouse click reliably transfers foreground, so hand the operator a fixed window
            // to do exactly that, then auto-start (NOT on a keypress — that would refocus the
            // terminal). See README "Known blocker: OS foreground focus".
            const secs = Number(process.env.SR_AUDIT_PAUSE_SECS) || 10;
            console.log(`\n[sr-audit] Edge is open (maximized). >>> CLICK the Edge window NOW <<< to give it OS foreground focus.`);
            console.log(`[sr-audit] The NVDA sweep auto-starts in ${secs}s. After you click Edge, do NOT touch the keyboard or mouse until the sweep finishes.\n`);
            for (let s = secs; s > 0; s--) { process.stdout.write(`\r  starting sweep in ${s}s ...  `); await sleep(1000); }
            process.stdout.write("\r  starting sweep now.            \n");
        }

        mkdirSync(resultsDir, { recursive: true });
        const report = { generated: new Date().toISOString(), baseUrl, protocolFile: "docs/superpowers/sr-test-protocol.md", components: {}, skipped: SKIPPED };

        if (SKIPPED.length > 0) {
            console.log(`[sr-audit] SKIPPED (no keyboard/SR surface, verified empirically): ${SKIPPED.map((s) => s.name).join(", ")}`);
        }

        for (const component of COMPONENTS) {
            console.log(`\n[sr-audit] === ${component.name} (/components/${component.slug}) ===`);
            const route = await gotoComponentPage(page, baseUrl, component.slug);

            const componentResult = { route, steps: [] };
            let focusOk = false;
            try {
                // Clear the accumulated speech log right before the action that
                // should trigger new speech — lastSpokenPhrase() below is just
                // spokenPhraseLog().at(-1), so without this a silent/delayed
                // announcement leaves the PREVIOUS utterance (page load, or the
                // prior component's last step) sitting there, which a keyword
                // match can accidentally satisfy as a false PASS.
                await nvda.clearSpokenPhraseLog();
                focusOk = await page.evaluate((sel) => {
                    const el = document.querySelector(sel);
                    if (!el) return false;
                    el.focus();
                    return document.activeElement === el;
                }, component.focusSelector);
            } catch (e) {
                console.warn(`[sr-audit] ${component.slug}: focus eval failed: ${e.message}`);
            }
            if (!focusOk) {
                console.warn(`[sr-audit] ${component.slug}: could not focus "${component.focusSelector}" — skipping, marking FAIL`);
                componentResult.steps.push({ row: 0, action: "focus", key: null, expected: [], actual: null, result: "FAIL — target element not found/focusable" });
                report.components[component.name] = componentResult;
                continue;
            }

            for (const step of component.steps) {
                if (step.action === "press") {
                    // Same reasoning as the focus clear above: this step's score
                    // must reflect speech caused by THIS press, not whatever the
                    // previous step (or the focus above) last spoke.
                    await nvda.clearSpokenPhraseLog();
                    await nvda.press(step.key);
                } else if (step.action === "focus") {
                    // NVDA never announces silent script-driven el.focus() on its own —
                    // explicitly ask it to report the current focus. An earlier attempt at
                    // this read the Edge TOOLBAR, but that was before the heading-click fix:
                    // OS keyboard focus genuinely WAS on the toolbar then, so NVDA reported
                    // it accurately. With OS focus now seated in the content (heading click)
                    // and DOM focus on the target (el.focus() above), reportCurrentFocus
                    // announces the actual component element — turning row 1 from a known
                    // gap into a real per-component verification.
                    await nvda.clearSpokenPhraseLog();
                    await nvda.perform(nvda.keyboardCommands.reportCurrentFocus);
                }
                await sleep(600);
                const actual = await nvda.lastSpokenPhrase();
                let result;
                const match = fuzzyContains(actual, step.expected, step.matchMode);
                if (step.action === "focus" && (!actual || actual.trim().length === 0)) {
                    // Even with an explicit reportCurrentFocus, some focus targets can yield
                    // no speech (e.g. NVDA is still settling after navigation). Score that as
                    // the known silent-focus case rather than a component FAIL — the
                    // navigation rows below still carry per-component announcement signal.
                    result = "SILENT-FOCUS (reportCurrentFocus produced no speech)";
                } else if (match === null) {
                    result = actual && actual.trim().length > 0 ? `LOGGED — "${actual}"` : "LOGGED — (empty)";
                } else {
                    const quantifier = step.matchMode === "all" ? "all of" : "one of";
                    result = match ? "PASS" : `FAIL — expected ${quantifier} [${step.expected.join(", ")}], got "${actual}"`;
                }
                console.log(`  row ${step.row} (${step.action}${step.key ? " " + step.key : ""}): ${result}`);
                componentResult.steps.push({ row: step.row, action: step.action, key: step.key, expected: step.expected, actual, result });
            }
            report.components[component.name] = componentResult;
        }

        if (allMode) {
            const allSlugs = await getAllSlugs(baseUrl);
            const covered = new Set(COMPONENTS.map((c) => c.slug));
            const genericSlugs = allSlugs.filter((s) => !covered.has(s));
            console.log(`\n[sr-audit] --all: ${allSlugs.length} total component pages, ${covered.size} covered by the detailed sweep above, generic-probing the remaining ${genericSlugs.length}`);

            report.generic = { totalSlugs: allSlugs.length, coveredByDetailedSweep: [...covered].sort(), swept: {} };
            const counts = { spoke: 0, silent: 0, "no-focusable": 0, error: 0 };

            for (const slug of genericSlugs) {
                try {
                    await gotoComponentPage(page, baseUrl, slug, { blazorTimeoutMs: 8000, settleMs: 300 });
                    const result = await runGenericProbe(page, slug);
                    report.generic.swept[slug] = result;
                    counts[result.classification] = (counts[result.classification] || 0) + 1;
                    const line = result.classification === "no-focusable"
                        ? "no focusable element in demo area"
                        : result.announcements.map((a) => `${a.key}: "${a.actual || "(empty)"}"`).join(" | ");
                    console.log(`[sr-audit] --all ${slug}: [${result.classification}] ${line}`);
                } catch (e) {
                    console.warn(`[sr-audit] --all ${slug}: ERROR — ${e.message}`);
                    report.generic.swept[slug] = { error: e.message, classification: "error" };
                    counts.error += 1;
                }
            }

            console.log(`\n[sr-audit] --all summary (${genericSlugs.length} pages generic-probed): spoke=${counts.spoke} silent=${counts.silent} no-focusable=${counts["no-focusable"]} error=${counts.error}`);
        }

        const date = new Date().toISOString().slice(0, 10);
        const outPath = join(resultsDir, `nvda-${date}.json`);
        writeFileSync(outPath, JSON.stringify(report, null, 2));
        console.log(`\n[sr-audit] wrote ${outPath}`);
    } finally {
        // Runs on both the success path AND any thrown error above — mirrors
        // run.mjs's docs-server try/finally in scripts/a11y-audit for the same
        // reason: a partially-set-up run must not leak processes/windows.
        if (nvdaStarted) {
            try { await nvda.stop(); } catch (e) { console.warn(`[sr-audit] nvda.stop() failed: ${e.message}`); }
        }
        if (browser) {
            try { await browser.close(); } catch (e) { console.warn(`[sr-audit] browser.close() failed: ${e.message}`); }
        }
        if (dotnetProc) {
            dotnetProc.kill();
            if (process.platform === "win32" && dotnetProc.pid) {
                spawn("taskkill", ["/pid", String(dotnetProc.pid), "/T", "/F"], { stdio: "ignore", shell: true });
            }
        }
    }
}

main().catch((e) => {
    console.error("[sr-audit] fatal:", e);
    process.exit(1);
});
