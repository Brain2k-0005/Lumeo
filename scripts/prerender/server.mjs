// scripts/prerender/server.mjs
//
// Minimal static server used by prerender.mjs to serve the dotnet publish
// output during crawl. Single export: startServer(rootDir, port = 4300).
// Returns { url, close() }.
//
// SPA fallback: any route that doesn't resolve to a file returns index.html
// (200) — same behavior as Cloudflare Pages' _redirects, so Blazor routing
// boots correctly for every URL we crawl.

import { createServer } from 'node:http';
import sirv from 'sirv';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

export function startServer(rootDir, port = 4300) {
    const indexPath = join(rootDir, 'index.html');
    if (!existsSync(indexPath)) {
        throw new Error(`[prerender/server] index.html not found at ${indexPath} — did you pass wwwroot/ (not the project root)?`);
    }
    const indexHtml = readFileSync(indexPath, 'utf8');
    const serve = sirv(rootDir, { dev: true, etag: true, single: false });

    const server = createServer((req, res) => {
        serve(req, res, () => {
            res.statusCode = 200;
            res.setHeader('Content-Type', 'text/html; charset=utf-8');
            res.end(indexHtml);
        });
    });

    return new Promise((resolve, reject) => {
        server.on('error', reject);
        server.listen(port, '127.0.0.1', () => {
            resolve({
                url: `http://127.0.0.1:${port}`,
                close: () => new Promise(r => server.close(r)),
            });
        });
    });
}
