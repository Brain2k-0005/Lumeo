#!/usr/bin/env node
// scripts/sr-audit/run.mjs
//
// Guidepup-driven NVDA automation of docs/superpowers/sr-test-protocol.md
// for the top-5 highest-keyboard-surface components in that protocol
// (DataGrid, FileManager, Tabs, Calendar, Cascader — ranked #1-#5 in the
// protocol's own "top 20" list; the task brief's "Button, Dialog, Combobox,
// Tabs, DataGrid" does not match the protocol, which has no Button/Dialog
// rows at all, so this substitutes the actual top-5 the protocol documents).
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
//
// Writes results/nvda-<date>.json (PASS/PARTIAL/FAIL per step, actual vs
// expected spoken text) and prints the same summary to stdout.

import { nvda } from "@guidepup/guidepup";
import { chromium } from "playwright-core";
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
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
const PORT = process.env.SR_AUDIT_PORT || 5292;

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
            { row: 1, action: "focus", key: null, expected: ["table", "grid"] },
            { row: 2, action: "press", key: "ArrowRight", expected: [] }, // no fixed text; logged for manual eyeball
            { row: 3, action: "press", key: "ArrowDown", expected: [] },
        ],
    },
    {
        name: "FileManager",
        slug: "file-manager",
        // Audited against the same "does .focus() actually land here" question
        // as DataGrid/Calendar/Cascader above: the `[role="tree"]` container
        // itself has no tabindex (fine, same as DataGrid's grid container) —
        // but unlike DataGrid, FileManager's `role="treeitem"` rows ALSO carry
        // no tabindex and no keydown handling at all (mouse-only; confirmed in
        // FileManager.razor). This is a real product-level keyboard-nav gap in
        // the component, not a selector bug this script can work around — a
        // FAIL here is an accurate signal, tracked separately from this PR.
        focusSelector: '[role="tree"]',
        steps: [
            { row: 1, action: "focus", key: null, expected: ["tree"] },
            { row: 2, action: "press", key: "ArrowDown", expected: [] },
            { row: 3, action: "press", key: "ArrowRight", expected: ["expand"] },
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
            { row: 1, action: "focus", key: null, expected: ["grid", "table"] },
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
            { row: 1, action: "focus", key: null, expected: ["button", "combobox"], matchMode: "all" },
            { row: 2, action: "press", key: "Enter", expected: ["menu"] },
            { row: 7, action: "press", key: "Escape", expected: [] },
        ],
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

        mkdirSync(resultsDir, { recursive: true });
        const report = { generated: new Date().toISOString(), baseUrl, protocolFile: "docs/superpowers/sr-test-protocol.md", components: {} };

        for (const component of COMPONENTS) {
            console.log(`\n[sr-audit] === ${component.name} (/components/${component.slug}) ===`);
            const route = `/components/${component.slug}`;
            await page.goto(baseUrl + route, { waitUntil: "load", timeout: 60_000 });
            try {
                await page.waitForFunction(() => document.documentElement.dataset.blazorReady === "true", { timeout: 30_000 });
            } catch {
                console.warn(`[sr-audit] ${component.slug}: blazorReady never set within 30s — continuing anyway`);
            }
            await sleep(1000);

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
                }
                await sleep(600);
                const actual = await nvda.lastSpokenPhrase();
                let result;
                const match = fuzzyContains(actual, step.expected, step.matchMode);
                if (match === null) {
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
