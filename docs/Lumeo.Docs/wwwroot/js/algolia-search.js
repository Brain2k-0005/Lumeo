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

// Tokenised, ranked local search. Scores each entry against every query
// token and returns the top N by combined score. Empirically this is
// <2ms over the full 173-item index, so we don't need to debounce hard.
//
//   prefix in title       → 100   ("dat" → "DataGrid" wins over "DropdownMenu")
//   substring in title    →  60
//   prefix in category    →  30
//   substring in summary  →  20
//   substring in category →  10
//
// Multi-word queries ("data grid", "form input") AND across tokens —
// every token has to match somewhere, but score is summed so an entry
// matching multiple tokens ranks higher than one matching just one.
function localSearch(query, items) {
    const tokens = query.toLowerCase().split(/\s+/).filter(t => t.length > 0);
    if (tokens.length === 0) return [];
    const scored = [];
    for (const item of items) {
        const title = (item.title ?? '').toLowerCase();
        const cat = (item.category ?? '').toLowerCase();
        const summary = (item.summary ?? '').toLowerCase();
        let total = 0;
        let everyTokenHit = true;
        for (const t of tokens) {
            let hit = 0;
            if (title.startsWith(t))      hit = 100;
            else if (title.includes(t))   hit = 60;
            else if (cat.startsWith(t))   hit = 30;
            else if (summary.includes(t)) hit = 20;
            else if (cat.includes(t))     hit = 10;
            if (hit === 0) { everyTokenHit = false; break; }
            total += hit;
        }
        if (everyTokenHit) scored.push({ item, score: total });
    }
    scored.sort((a, b) => b.score - a.score || a.item.title.localeCompare(b.item.title));
    return scored.slice(0, 12).map(s => s.item);
}

window.lumeoAlgolia = {
    async search(query) {
        if (!query || query.length < 1) return [];
        // Local-first: the index is ~30 KB, prefetched on script load,
        // and ranked search is sub-2 ms — instant feedback beats a
        // network round-trip every time. Algolia is only used when the
        // local index has no hits at all, as a wider net for typo /
        // synonym handling.
        const items = await getLocalIndex();
        const localHits = localSearch(query, items);
        if (localHits.length > 0) return localHits;

        const client = await getClient();
        if (client) {
            try {
                const { results } = await client.search({
                    requests: [{ indexName: 'lumeo_docs', query, hitsPerPage: 12 }],
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
                console.warn('Algolia search failed', e);
            }
        }
        return [];
    },
};
