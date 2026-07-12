// Shared helpers for the Phase 5 perf-fact scripts. Node ESM (repo root
// package.json doesn't set "type": "module", so these files live in
// scripts/perf/ with their own package.json that does).
import { chromium } from 'playwright';
import os from 'node:os';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const BASE_URL = process.env.LUMEO_PERF_BASE_URL || 'http://localhost:5287';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const RESULTS_DIR = path.resolve(__dirname, '..', 'results');

export function median(nums) {
  const sorted = [...nums].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
}

export async function launchBrowser() {
  return chromium.launch({ headless: true });
}

export async function machineInfo(browser) {
  const cpus = os.cpus();
  return {
    platform: os.platform(),
    arch: os.arch(),
    cpuModel: cpus[0]?.model ?? 'unknown',
    cpuCores: cpus.length,
    totalMemGiB: Math.round((os.totalmem() / (1024 ** 3)) * 10) / 10,
    nodeVersion: process.version,
    browser: await browser.version(),
  };
}

export function writeResult(filename, data) {
  fs.mkdirSync(RESULTS_DIR, { recursive: true });
  const full = path.join(RESULTS_DIR, filename);
  fs.writeFileSync(full, JSON.stringify(data, null, 2) + '\n', 'utf8');
  console.log(`Wrote ${path.relative(process.cwd(), full)}`);
  return full;
}

// A fresh, isolated BrowserContext per run — not just a fresh page in the
// SAME context — isolates each measurement from GC/JIT warm-up, DOM/timer
// leftovers, AND network-level state (HTTP cache, WASM/script cache,
// cookies, storage) carried over from a previous run. browser.newPage() with
// no context reuses one shared context for every run, so run 2-5 would see a
// warm HTTP/WASM cache from run 1 — closer to what a real user's first visit
// sees, matching README.md's "no warm-up state carries over between runs".
export async function withFreshPage(browser, fn) {
  const context = await browser.newContext();
  const page = await context.newPage();
  try {
    return await fn(page);
  } finally {
    await context.close();
  }
}

export function nowIso() {
  return new Date().toISOString();
}
