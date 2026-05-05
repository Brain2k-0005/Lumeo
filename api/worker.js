// Lumeo preset API — Cloudflare Worker + KV.
//
// Endpoints:
//   POST /preset          → body = JSON config, returns { id: "b4Ndd7" }
//   GET  /preset/:id      → returns stored JSON (null on not-found)
//   OPTIONS /*            → CORS preflight
//
// Storage: Cloudflare KV namespace bound as `PRESETS` (see wrangler.toml).
// IDs: 6-char base62, collision-retried up to 3 times.
// Quotas (free tier): 100k requests/day + 1k KV writes/day + 100k KV reads/day.
// More than enough for a preset-sharing API unless someone abuses it.

const BASE62 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
const MAX_BODY_BYTES = 8192; // presets are tiny; reject oversized payloads outright
const ID_LEN = 6;
const COLLISION_RETRIES = 8;

// Canonical JSON: keys sorted alphabetically so order doesn't affect the hash.
// Same logical config → same canonical string → same hash → same ID.
function canonicalize(obj) {
    if (obj === null || typeof obj !== "object" || Array.isArray(obj)) return JSON.stringify(obj);
    const keys = Object.keys(obj).sort();
    const parts = keys.map(k => JSON.stringify(k) + ":" + canonicalize(obj[k]));
    return "{" + parts.join(",") + "}";
}

// Content-addressed ID: SHA-256 of the canonical JSON (optionally salted to
// dodge a real collision), truncated to 36 bits, base62-encoded to 6 chars.
// Same config + same salt → same ID every time.
async function contentId(canonical, salt = 0) {
    const input = salt === 0 ? canonical : canonical + "\x00" + salt;
    const bytes = new TextEncoder().encode(input);
    const hashBuf = await crypto.subtle.digest("SHA-256", bytes);
    const hashBytes = new Uint8Array(hashBuf);
    let s = "";
    // Read the first 5 bytes → 40 bits → take 6 base62 chars.
    let acc = 0n;
    for (let i = 0; i < 5; i++) acc = (acc << 8n) | BigInt(hashBytes[i]);
    for (let i = 0; i < ID_LEN; i++) {
        s += BASE62[Number(acc % 62n)];
        acc /= 62n;
    }
    return s;
}

function cors(headers = {}) {
    return {
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type",
        "Access-Control-Max-Age": "86400",
        ...headers,
    };
}

function json(body, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: cors({ "Content-Type": "application/json" }),
    });
}

export default {
    async fetch(request, env) {
        const url = new URL(request.url);
        const path = url.pathname.replace(/\/+$/, "");

        if (request.method === "OPTIONS") {
            return new Response(null, { status: 204, headers: cors() });
        }

        // POST /preset — store a new preset.
        if (request.method === "POST" && path === "/preset") {
            // Fast path: trust an honest content-length header to reject before reading.
            const cl = parseInt(request.headers.get("content-length") || "", 10);
            if (Number.isFinite(cl) && cl > MAX_BODY_BYTES) {
                return json({ error: "payload_too_large" }, 413);
            }
            // Authoritative check: read the actual bytes and enforce the cap on byteLength,
            // since the header is client-supplied and may be missing or wrong.
            const buf = await request.arrayBuffer();
            if (buf.byteLength > MAX_BODY_BYTES) {
                return json({ error: "payload_too_large" }, 413);
            }
            let body;
            try { body = JSON.parse(new TextDecoder().decode(buf)); }
            catch { return json({ error: "invalid json" }, 400); }
            if (!body || typeof body !== "object") {
                return json({ error: "expected object" }, 400);
            }
            // Validate: presets should be small flat objects of scalar values.
            const entries = Object.entries(body);
            if (entries.length > 32) return json({ error: "too many keys" }, 400);
            for (const [k, v] of entries) {
                if (typeof k !== "string" || k.length > 64) return json({ error: "bad key" }, 400);
                if (v !== null && typeof v !== "string" && typeof v !== "number" && typeof v !== "boolean") {
                    return json({ error: `bad value for ${k}` }, 400);
                }
            }
            // Content-addressed: same config always yields the same ID. If the slot is empty
            // or already holds this exact canonical payload we're done (idempotent — saves a
            // KV write). On a real hash collision (different payload at the same ID, ~1 in 2^36
            // per pair) we re-hash with an incrementing salt to land on a fresh ID instead of
            // silently overwriting the existing preset. Bounded so worst-case latency stays sane.
            const canonicalValue = canonicalize(body);
            let id;
            let placed = false;
            for (let salt = 0; salt < COLLISION_RETRIES; salt++) {
                id = await contentId(canonicalValue, salt);
                const existing = await env.PRESETS.get(id);
                if (existing === null) {
                    await env.PRESETS.put(id, canonicalValue, {
                        // 2 years — plenty for sharing a preset in an issue / forum post.
                        expirationTtl: 60 * 60 * 24 * 365 * 2,
                    });
                    placed = true;
                    break;
                }
                if (existing === canonicalValue) {
                    // Same payload already stored under this id — return idempotently.
                    placed = true;
                    break;
                }
                // Different payload at this id: real collision — try the next salt.
            }
            if (!placed) {
                return json({ error: "id_collision_exhausted" }, 409);
            }
            return json({ id });
        }

        // GET /preset/:id — fetch a stored preset.
        const match = path.match(/^\/preset\/([0-9A-Za-z]{4,32})$/);
        if (request.method === "GET" && match) {
            const value = await env.PRESETS.get(match[1]);
            if (!value) return json({ error: "not found" }, 404);
            return new Response(value, {
                status: 200,
                headers: cors({ "Content-Type": "application/json", "Cache-Control": "public, max-age=300" }),
            });
        }

        // Tiny healthcheck.
        if (request.method === "GET" && path === "") {
            return json({ service: "lumeo-preset-api", ok: true });
        }

        return json({ error: "not found" }, 404);
    },
};
