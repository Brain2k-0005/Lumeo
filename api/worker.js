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

function randomId() {
    let s = "";
    const r = new Uint8Array(ID_LEN);
    crypto.getRandomValues(r);
    for (let i = 0; i < ID_LEN; i++) s += BASE62[r[i] % 62];
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
            if (request.headers.get("content-length") > MAX_BODY_BYTES) {
                return json({ error: "payload too large" }, 413);
            }
            let body;
            try { body = await request.json(); }
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
            // Try 3 times in case of ID collision (astronomically unlikely at 56bits entropy).
            for (let attempt = 0; attempt < 3; attempt++) {
                const id = randomId();
                const existing = await env.PRESETS.get(id);
                if (existing) continue;
                await env.PRESETS.put(id, JSON.stringify(body), {
                    // 2 years — plenty for sharing a preset in an issue / forum post.
                    expirationTtl: 60 * 60 * 24 * 365 * 2,
                });
                return json({ id });
            }
            return json({ error: "could not allocate id" }, 503);
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
