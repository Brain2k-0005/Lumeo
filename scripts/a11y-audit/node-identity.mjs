#!/usr/bin/env node
// scripts/a11y-audit/node-identity.mjs
//
// Shared by check-baseline.mjs and gen-baseline.mjs so their notion of "the
// same violation node" can never drift between the two scripts — if it did,
// every baselined entry would silently stop matching and the gate would
// flag all known debt as brand new.
//
// A violation's target selector and outerHTML are NOT stable across runs:
// Lumeo components mint a fresh random id (and everything that references
// it — aria-controls, aria-describedby, for, ...) on every render, so e.g.
// a DataGrid's select-trigger button gets a different `#select-trigger-<hex>`
// target and a different `id="select-trigger-<hex>..."` in its html each
// time the docs page boots, even though it's structurally the exact same
// offending node. Hashing the raw target/html would therefore mark every
// node as "new" on every single run — useless.
//
// Instead we strip anything that LOOKS like a generated id (a run of 4+
// hex-only characters — the axe report itself often truncates these to a
// short prefix like "3a7f0..." before the string is even captured, so the
// pattern must match short runs too) and hash what's left: tag, attributes,
// static classes, role, aria-* flags, text. Two nodes with the same shape
// after stripping are the same violation, even across renders. Two nodes
// that differ only in a rendered id (all 50 DataGrid select-triggers, say)
// still collapse to as many distinct SHAPES as there are structurally
// distinct offenders (e.g. one shape per column/demo variant), not 1 and
// not 50 — which is what makes the shape set useful for detecting "a
// different node started failing" instead of just "the count changed".
import { createHash } from 'node:crypto';

const VOLATILE_ID_RUN = /[0-9a-f]{4,}/gi;

export function normalizeNodeShape(target, html) {
    const targetText = Array.isArray(target) ? target.join(' ') : String(target ?? '');
    const raw = `${targetText} ${html ?? ''}`;
    return raw.replace(VOLATILE_ID_RUN, '‹id›');
}

// Short (12 hex char) digest — plenty of collision resistance for the low
// hundreds of nodes a single component/rule pair ever has, and short enough
// to stay readable as a committed baseline.json array entry.
export function nodeShapeHash(target, html) {
    return createHash('sha1').update(normalizeNodeShape(target, html)).digest('hex').slice(0, 12);
}
