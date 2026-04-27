// Thin wrapper around algoliasearch lite SDK (loaded from esm.sh on first use).
// Exposes window.lumeoAlgolia.search(query) for Blazor JS interop.
// Falls back to the local index if Algolia credentials are missing or fetch fails.

let _client = null;
let _localIndex = null;
let _localIndexPromise = null;
let _disabled = false;

async function getClient() {
    if (_client || _disabled) return _client;
    const meta = (n) => document.querySelector(`meta[name="${n}"]`)?.getAttribute('content') ?? null;
    const appId = meta('algolia-app-id');
    const searchKey = meta('algolia-search-key');
    if (!appId || !searchKey || appId.startsWith('__') || searchKey.startsWith('__') || window.__LUMEO_DISABLE_ALGOLIA) {
        _disabled = true;
        return null;
    }
    const { liteClient } = await import('https://esm.sh/algoliasearch/lite');
    _client = liteClient(appId, searchKey);
    return _client;
}

// Eagerly pre-fetch the local index so the first keystroke is instant.
function prefetchLocalIndex() {
    if (_localIndex || _localIndexPromise) return;
    _localIndexPromise = fetch('/registry-search.json')
        .then(r => r.json())
        .then(data => { _localIndex = data; })
        .catch(() => { /* silently ignore — will retry lazily */ });
}

async function getLocalIndex() {
    if (_localIndex) return _localIndex;
    // Await the in-flight prefetch if it's already running
    if (_localIndexPromise) { await _localIndexPromise; return _localIndex ?? []; }
    const res = await fetch('/registry-search.json');
    _localIndex = await res.json();
    return _localIndex;
}

// Kick off the prefetch immediately on script load.
prefetchLocalIndex();

function localSearch(query, items) {
    const q = query.toLowerCase();
    return items
        .filter(i => i.title.toLowerCase().includes(q) || i.category.toLowerCase().includes(q))
        .slice(0, 8);
}

window.lumeoAlgolia = {
    async search(query) {
        if (!query || query.length < 1) return [];
        const client = await getClient();
        if (client) {
            try {
                const { results } = await client.search({
                    requests: [{ indexName: 'lumeo_docs', query, hitsPerPage: 8 }],
                });
                return results[0].hits.map(h => ({
                    id: h.objectID,
                    title: h.title,
                    summary: h.summary,
                    category: h.category,
                    type: h.type,
                    url: h.url,
                }));
            } catch (e) {
                console.warn('Algolia search failed, falling back to local index', e);
            }
        }
        const items = await getLocalIndex();
        return localSearch(query, items);
    },
};
