// scripts/prerender/og-cards.mjs
//
// Usage: node og-cards.mjs <publish-dir>
//
// Generates one 1280×640 social preview PNG per route using Satori + resvg-js,
// AND injects per-route <meta name="description"> (and og:/twitter: variants)
// extracted from each page's <PageHeader Description="..." /> in Razor source.
//
// For each route in sitemap.xml:
//   1. Read its prerendered HTML for the <title>
//   2. Render an OG card SVG via Satori (flex layout, gradients, typography)
//   3. Rasterize SVG → PNG via @resvg/resvg-js
//   4. Write to <publish-dir>/wwwroot/og/<slug>.png
//   5. Rewrite og:image / twitter:image + description meta tags in the route's index.html

import satori from 'satori';
import { Resvg } from '@resvg/resvg-js';
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync, statSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const publishDir = process.argv[2];
if (!publishDir) {
    console.error('usage: node og-cards.mjs <publish-dir>');
    process.exit(1);
}
const wwwroot = resolve(publishDir, 'wwwroot');
const ogDir = join(wwwroot, 'og');
mkdirSync(ogDir, { recursive: true });

const SITE = 'https://lumeo.nativ.sh';

// Fonts — loaded once. Satori requires explicit font data (no system lookups).
const interRegular = readFileSync(join(__dirname, 'node_modules/@fontsource/inter/files/inter-latin-400-normal.woff'));
const interBold    = readFileSync(join(__dirname, 'node_modules/@fontsource/inter/files/inter-latin-700-normal.woff'));

const sitemapXml = readFileSync(join(wwwroot, 'sitemap.xml'), 'utf8');
const routes = [...sitemapXml.matchAll(/<loc>([^<]+)<\/loc>/g)]
    .map(m => new URL(m[1]).pathname)
    .filter(Boolean);

console.log(`[og-cards] ${routes.length} routes`);

// ----- Build a map of route → description from Razor source -----
//
// Most docs pages declare their copy as <PageHeader Title="..." Description="..." />.
// Harvesting that at CI time gives Google real per-page copy to show in SERP
// snippets instead of the site-wide fallback description.
const pagesDir = resolve(__dirname, '../../docs/Lumeo.Docs/Pages');

function walkRazor(dir) {
    const out = [];
    for (const entry of readdirSync(dir)) {
        const full = join(dir, entry);
        const stat = statSync(full);
        if (stat.isDirectory()) out.push(...walkRazor(full));
        else if (entry.endsWith('.razor')) out.push(full);
    }
    return out;
}

function buildDescriptionMap() {
    const map = {};
    const files = existsSync(pagesDir) ? walkRazor(pagesDir) : [];
    for (const file of files) {
        let content;
        try { content = readFileSync(file, 'utf8'); } catch { continue; }
        // Strip leading BOM if present.
        if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);

        const pageMatch = content.match(/^@page\s+"([^"]+)"/m);
        if (!pageMatch) continue;
        const route = pageMatch[1];

        // <PageHeader ... Description="..." /> — the attribute can sit on any
        // line relative to the tag, so scan across the element.
        const phMatch = content.match(/<PageHeader\b[^>]*?\bDescription\s*=\s*"([^"]+)"/s);
        if (phMatch) {
            map[route] = phMatch[1].trim();
        }
    }
    return map;
}

const descriptions = buildDescriptionMap();
console.log(`[og-cards] ${Object.keys(descriptions).length} per-route descriptions extracted`);

function slugFor(route) {
    if (route === '/') return 'home';
    return route.replace(/^\/+|\/+$/g, '').replace(/\//g, '-');
}

function categoryFor(route) {
    if (route.startsWith('/components/')) return { label: 'Component', tagline: 'Blazor component documentation' };
    if (route.startsWith('/blocks/'))     return { label: 'Block',     tagline: 'Copy-paste UI block' };
    if (route.startsWith('/docs/'))       return { label: 'Docs',      tagline: 'Lumeo documentation' };
    if (route.startsWith('/patterns'))    return { label: 'Pattern',   tagline: 'Composition pattern' };
    return { label: 'UI Library', tagline: 'Modern Blazor component library' };
}

function extractTitle(html) {
    const m = html.match(/<title>([^<]+)<\/title>/);
    if (!m) return '';
    return m[1]
        .replace(/<!--!-->/g, '')                  // raw Blazor markers
        .replace(/&lt;!--!--&gt;/g, '')            // entity-encoded Blazor markers
        .replace(/&amp;/g, '&').replace(/&quot;/g, '"').replace(/&#39;/g, "'")
        .replace(/\s*[—–-]\s*Lumeo\s*$/i, '')      // trailing " — Lumeo"
        .trim();
}

// Satori JSX-like object-tree template.
function cardTree({ title, subtitle, category }) {
    return {
        type: 'div',
        props: {
            style: {
                width: 1280, height: 640, display: 'flex', flexDirection: 'column',
                justifyContent: 'space-between', padding: '72px 80px',
                fontFamily: 'Inter', color: '#fafafa',
                backgroundColor: '#0a0a0a',
                backgroundImage:
                    'radial-gradient(circle at 18% 22%, rgba(59,130,246,0.26) 0%, rgba(10,10,10,0) 42%),' +
                    'radial-gradient(circle at 85% 80%, rgba(139,92,246,0.22) 0%, rgba(10,10,10,0) 45%)',
            },
            children: [
                // Top row: brand + category pill
                {
                    type: 'div',
                    props: {
                        style: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
                        children: [
                            {
                                type: 'div',
                                props: {
                                    style: { display: 'flex', alignItems: 'center', gap: 14, fontSize: 28, fontWeight: 700, letterSpacing: '-0.02em' },
                                    children: [
                                        // Simple Lumeo mark: outer ring + inner dot (as SVG).
                                        {
                                            type: 'svg',
                                            props: {
                                                width: 36, height: 36, viewBox: '0 0 32 32',
                                                xmlns: 'http://www.w3.org/2000/svg',
                                                children: [
                                                    { type: 'circle', props: { cx: 16, cy: 16, r: 9, fill: 'none', stroke: '#fafafa', strokeWidth: 2, opacity: 0.55 } },
                                                    { type: 'circle', props: { cx: 16, cy: 16, r: 3, fill: '#fafafa' } },
                                                ],
                                            },
                                        },
                                        'Lumeo',
                                    ],
                                },
                            },
                            {
                                type: 'div',
                                props: {
                                    style: {
                                        padding: '8px 18px', borderRadius: 999,
                                        backgroundColor: 'rgba(255,255,255,0.08)',
                                        border: '1px solid rgba(255,255,255,0.14)',
                                        fontSize: 15, fontWeight: 500, color: '#d4d4d4',
                                        textTransform: 'uppercase', letterSpacing: '0.1em',
                                    },
                                    children: category,
                                },
                            },
                        ],
                    },
                },

                // Middle: title + subtitle
                {
                    type: 'div',
                    props: {
                        style: { display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 1050 },
                        children: [
                            {
                                type: 'div',
                                props: {
                                    style: { fontSize: 76, fontWeight: 700, lineHeight: 1.02, letterSpacing: '-0.035em', color: '#fafafa' },
                                    children: title,
                                },
                            },
                            {
                                type: 'div',
                                props: {
                                    style: { fontSize: 26, color: '#a3a3a3', lineHeight: 1.4, fontWeight: 400 },
                                    children: subtitle,
                                },
                            },
                        ],
                    },
                },

                // Footer
                {
                    type: 'div',
                    props: {
                        style: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 17, letterSpacing: '0.02em' },
                        children: [
                            { type: 'div', props: { style: { color: '#a3a3a3', fontWeight: 500 }, children: 'lumeo.nativ.sh' } },
                            { type: 'div', props: { style: { color: '#525252' }, children: '130+ components · MIT · .NET 10' } },
                        ],
                    },
                },
            ],
        },
    };
}

function escapeAttr(s) {
    return String(s).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ---- Breadcrumb trail derived from route path ----
// "/components/button" → [ {name: "Home", url: /}, {name: "Components", url: /components/button}, {name: "Button", url: /components/button} ]
// Using the final route URL for intermediate crumbs (Lumeo has no /components index page).
function breadcrumbs(route, pageTitle) {
    const crumbs = [{ name: 'Home', url: `${SITE}/` }];
    if (route === '/') return crumbs;
    const parts = route.replace(/^\/+|\/+$/g, '').split('/');
    // Intermediate segment label — title-case the slug, e.g. "components" → "Components".
    for (let i = 0; i < parts.length - 1; i++) {
        const seg = parts[i];
        crumbs.push({
            name: seg.split('-').map(s => s[0].toUpperCase() + s.slice(1)).join(' '),
            url: `${SITE}${route}`,
        });
    }
    crumbs.push({ name: pageTitle, url: `${SITE}${route}` });
    return crumbs;
}

// ---- JSON-LD structured data ----
// Choose schema.org type based on route prefix.
function structuredDataFor(route, title, description) {
    const url = `${SITE}${route}`;
    const crumbs = breadcrumbs(route, title);

    const breadcrumbList = {
        '@type': 'BreadcrumbList',
        itemListElement: crumbs.map((c, i) => ({
            '@type': 'ListItem',
            position: i + 1,
            name: c.name,
            item: c.url,
        })),
    };

    let primary;
    if (route === '/') {
        primary = {
            '@type': 'SoftwareApplication',
            name: 'Lumeo',
            description,
            applicationCategory: 'DeveloperApplication',
            operatingSystem: 'Cross-platform',
            offers: { '@type': 'Offer', price: '0', priceCurrency: 'USD' },
            url,
            author: { '@type': 'Organization', name: 'Lumeo', url: SITE },
        };
    } else if (route.startsWith('/components/') || route.startsWith('/blocks/') || route.startsWith('/patterns')) {
        primary = {
            '@type': 'TechArticle',
            headline: title,
            description,
            url,
            image: `${SITE}/og${route === '/' ? '/home' : '/' + slugFor(route)}.png`,
            author: { '@type': 'Organization', name: 'Lumeo', url: SITE },
            publisher: { '@type': 'Organization', name: 'Lumeo', url: SITE, logo: { '@type': 'ImageObject', url: `${SITE}/favicon.ico` } },
            isPartOf: { '@type': 'WebSite', name: 'Lumeo', url: SITE },
        };
    } else {
        primary = {
            '@type': 'WebPage',
            name: title,
            description,
            url,
            isPartOf: { '@type': 'WebSite', name: 'Lumeo', url: SITE },
        };
    }

    return {
        '@context': 'https://schema.org',
        '@graph': [primary, breadcrumbList],
    };
}

function rewriteMeta(html, route, slug, title, description) {
    const imgUrl = `${SITE}/og/${slug}.png`;
    const canonicalUrl = `${SITE}${route}`;

    // og:image + twitter:image
    html = html.replace(/(<meta property="og:image" content=")[^"]+(")/i, `$1${imgUrl}$2`);
    html = html.replace(/(<meta name="twitter:image" content=")[^"]+(")/i, `$1${imgUrl}$2`);

    // Descriptions
    if (description) {
        const esc = escapeAttr(description);
        html = html.replace(/(<meta name="description" content=")[^"]*(")/i, `$1${esc}$2`);
        html = html.replace(/(<meta property="og:description" content=")[^"]*(")/i, `$1${esc}$2`);
        html = html.replace(/(<meta name="twitter:description" content=")[^"]*(")/i, `$1${esc}$2`);
    }

    // og:url — tell platforms the canonical URL for this resource.
    html = html.replace(/(<meta property="og:url" content=")[^"]*(")/i, `$1${canonicalUrl}$2`);

    // og:image:alt — accessibility + crawler signal.
    const altText = `Social preview card for ${title} — Lumeo Blazor component library`;
    const escAlt = escapeAttr(altText);
    if (/og:image:alt/i.test(html)) {
        html = html.replace(/(<meta property="og:image:alt" content=")[^"]*(")/i, `$1${escAlt}$2`);
    } else {
        html = html.replace(/(<meta property="og:image" content="[^"]*"\s*\/?>)/i, `$1\n    <meta property="og:image:alt" content="${escAlt}">`);
    }

    // Canonical <link> — inject after <base href=...> if absent; otherwise rewrite.
    if (/<link\s+rel=["']canonical["']/i.test(html)) {
        html = html.replace(/(<link\s+rel=["']canonical["']\s+href=")[^"]*(")/i, `$1${canonicalUrl}$2`);
    } else {
        html = html.replace(/(<base\s+href="[^"]*"\s*\/?>)/i, `$1\n    <link rel="canonical" href="${canonicalUrl}">`);
    }

    // JSON-LD structured data — strip any previous lumeo-seo block, then inject fresh.
    const jsonLd = JSON.stringify(structuredDataFor(route, title, description || ''));
    html = html.replace(/\s*<script\s+type="application\/ld\+json"\s+data-lumeo-seo[^>]*>[\s\S]*?<\/script>/gi, '');
    html = html.replace(/(<\/head>)/i, `    <script type="application/ld+json" data-lumeo-seo>${jsonLd}</script>\n$1`);

    return html;
}

const satoriOpts = {
    width: 1280,
    height: 640,
    fonts: [
        { name: 'Inter', data: interRegular, weight: 400, style: 'normal' },
        { name: 'Inter', data: interBold,    weight: 700, style: 'normal' },
    ],
};

const startAll = Date.now();
let ok = 0, fail = 0;

for (const route of routes) {
    try {
        const htmlPath = route === '/'
            ? join(wwwroot, 'index.html')
            : join(wwwroot, route, 'index.html');
        if (!existsSync(htmlPath)) {
            fail++;
            console.error(`✗ ${route} — no prerendered HTML`);
            continue;
        }

        const routeHtml = readFileSync(htmlPath, 'utf8');
        const title = extractTitle(routeHtml) || 'Lumeo';
        const cat = categoryFor(route);
        // Prefer the per-page PageHeader Description; fall back to the category
        // tagline so every route still gets *some* meaningful description.
        const description = descriptions[route] || cat.tagline;

        const svg = await satori(cardTree({ title, subtitle: cat.tagline, category: cat.label }), satoriOpts);
        const png = new Resvg(svg, { background: '#0a0a0a' }).render().asPng();

        const slug = slugFor(route);
        writeFileSync(join(ogDir, `${slug}.png`), png);
        writeFileSync(htmlPath, rewriteMeta(routeHtml, route, slug, title, description), 'utf8');

        ok++;
        if (ok % 40 === 0) console.log(`[og-cards] ${ok}/${routes.length}`);
    } catch (err) {
        fail++;
        console.error(`✗ ${route} — ${err.message}`);
    }
}

const secs = ((Date.now() - startAll) / 1000).toFixed(1);
console.log(`[og-cards] done: ${ok} ok, ${fail} failed, ${secs}s`);

// ---- llms.txt — emerging convention so ChatGPT / Perplexity / Claude can
//      locate canonical Lumeo documentation without scraping the full site. ----
const llmsLines = [
    '# Lumeo',
    '',
    '> A modern, accessible Blazor component library — 130+ production-ready components on Tailwind CSS v4, with AI primitives, motion, DataGrid, charts, and RTL/i18n support. MIT-licensed, .NET 10, 868 KB total package.',
    '',
    '## Key pages',
    '',
    `- [Introduction](${SITE}/docs/introduction): What Lumeo is and how it compares to MudBlazor/Radzen/AntBlazor`,
    `- [Getting Started](${SITE}/docs/templates): \`dotnet new\` templates and setup`,
    `- [CLI](${SITE}/docs/cli): \`lumeo add\` / \`lumeo init\` commands`,
    `- [Registry](${SITE}/docs/registry): Component registry for tooling`,
    `- [MCP server](${SITE}/docs/mcp): Model Context Protocol integration for AI agents`,
    `- [Accessibility](${SITE}/docs/accessibility): WCAG AA compliance notes`,
    `- [Theming](${SITE}/themes): Live theme customizer`,
    `- [Changelog](${SITE}/docs/changelog)`,
    '',
    '## Components',
    '',
];
for (const r of routes.filter(x => x.startsWith('/components/')).sort()) {
    const name = r.replace('/components/', '').split('-').map(s => s[0].toUpperCase() + s.slice(1)).join(' ');
    const desc = descriptions[r] || 'Blazor component';
    llmsLines.push(`- [${name}](${SITE}${r}): ${desc}`);
}
llmsLines.push('', '## Blocks (copy-paste UI)', '');
for (const r of routes.filter(x => x.startsWith('/blocks/')).sort()) {
    const name = r.replace('/blocks/', '').split('-').map(s => s[0].toUpperCase() + s.slice(1)).join(' ');
    const desc = descriptions[r] || 'Pre-built UI block';
    llmsLines.push(`- [${name}](${SITE}${r}): ${desc}`);
}
writeFileSync(join(wwwroot, 'llms.txt'), llmsLines.join('\n'), 'utf8');
console.log(`[og-cards] wrote llms.txt (${llmsLines.length} lines)`);

// Fail the step only if a large fraction fails (cards fall back to the
// site-wide /social-preview.png for routes whose PNG is missing).
if (fail > 0 && fail / (ok + fail) > 0.15) {
    process.exitCode = 1;
}
