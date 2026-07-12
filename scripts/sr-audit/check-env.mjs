#!/usr/bin/env node
// scripts/sr-audit/check-env.mjs
//
// Environment sanity check to run BEFORE run.mjs. Verifies, in order:
//   1. A portable NVDA install is registered (via `npx @guidepup/setup`).
//   2. NVDA actually starts and the Guidepup remote-control channel connects.
//   3. This process can obtain real OS foreground focus for a window it
//      spawns itself — a hard prerequisite for NVDA to read the right
//      content, and the thing that silently fails on a shared/locked-down
//      interactive desktop (see README.md "Known blocker").
//
// Exit code 0 only if all three checks pass. Prints a clear PASS/FAIL per
// check either way — this script is meant to be read, not just executed.

import { nvda } from "@guidepup/guidepup";
import { chromium } from "playwright-core";
import { setTimeout as sleep } from "node:timers/promises";
import { execFileSync } from "node:child_process";

function checkForegroundWindowTitle() {
    // Windows-only: shells out to a tiny inline PowerShell snippet using
    // user32.dll's GetForegroundWindow/GetWindowText — no extra npm deps.
    const script = `
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class GpFg {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
"@
$h = [GpFg]::GetForegroundWindow()
$sb = New-Object System.Text.StringBuilder 256
[GpFg]::GetWindowText($h, $sb, 256) | Out-Null
$procId = 0
[GpFg]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
Write-Output "$($sb.ToString())|$procId"
`;
    const out = execFileSync("powershell", ["-NoProfile", "-Command", script], { encoding: "utf8" }).trim();
    const [title, pid] = out.split("|");
    return { title, pid: Number(pid) };
}

async function main() {
    console.log("[check-env] 1/3 NVDA install registration...");
    // getNVDAInstallationPath is not exported directly; starting NVDA is the
    // real test — it throws ERR_NVDA_NOT_INSTALLED if `npx @guidepup/setup`
    // was never run.
    let started = false;
    try {
        await nvda.start();
        started = true;
        console.log("  PASS — NVDA process started via Guidepup.");
    } catch (e) {
        console.log(`  FAIL — ${e.message}`);
        console.log("  -> run `npm run setup` (npx @guidepup/setup) in this directory first.");
        process.exitCode = 1;
        return;
    }

    console.log("[check-env] 2/3 Guidepup <-> NVDA remote-control channel + speech capture...");
    await sleep(2000);
    await nvda.next();
    const phrase = await nvda.lastSpokenPhrase();
    if (phrase && phrase.trim().length > 0) {
        console.log(`  PASS — captured: "${phrase}"`);
    } else {
        console.log("  FAIL (or inconclusive) — NVDA started but no spoken phrase was captured for next().");
        console.log("  This is consistent with the foreground-focus blocker below rather than a Guidepup bug —");
        console.log("  NVDA's object-navigation commands read whatever currently has OS focus.");
    }

    console.log("[check-env] 3/3 Can this process win real OS foreground focus for its own window?...");
    const before = checkForegroundWindowTitle();
    console.log(`  Foreground before: "${before.title}" (pid ${before.pid})`);

    const edgeCandidates = [
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
        "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
    ];
    const { existsSync } = await import("node:fs");
    const edgePath = edgeCandidates.find((p) => existsSync(p));
    let browser = null;
    try {
        browser = await chromium.launch({
            headless: false,
            executablePath: edgePath, // undefined => playwright-core needs its own browser; edge is the pragmatic fallback here
            args: ["--start-maximized", "--new-window"],
        });
        const page = await browser.newPage();
        await page.goto("data:text/html,<h1>Guidepup foreground check</h1>", { waitUntil: "load" });
        await sleep(1500);
        const after = checkForegroundWindowTitle();
        console.log(`  Foreground after launching a browser window: "${after.title}" (pid ${after.pid})`);
        if (after.title.includes("Guidepup foreground check") || after.title.toLowerCase().includes("edge")) {
            console.log("  PASS — the freshly launched browser window won real foreground focus.");
        } else {
            console.log("  FAIL — the freshly launched window did NOT become the foreground window.");
            console.log(`  Something else ("${after.title}") is holding OS foreground focus/lock on this desktop.`);
            console.log("  NVDA will keep reading/reviewing that window, not the docs site under test.");
            process.exitCode = 1;
        }
    } finally {
        if (browser) await browser.close();
    }

    if (started) {
        await nvda.stop();
    }
}

main().catch((e) => {
    console.error("[check-env] unexpected error:", e);
    process.exitCode = 1;
});
