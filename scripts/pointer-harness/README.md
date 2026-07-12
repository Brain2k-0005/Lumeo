# Pointer harness — cross-engine real-pointer tests for `components.js`

Drives `src/Lumeo/wwwroot/js/components.js` directly with genuine Playwright
`PointerEvent`/`page.mouse` input against a bare HTML fixture (`harness.html`)
— no .NET/Blazor host involved. `.NET` interop calls are stubbed via
`window.__fakeDotNet` / `window.__makeDeferredDotNet` (see `harness.html`),
so assertions exercise the real DOM/CSS/pointer-capture contract the JS
module has with the browser, across three engines.

## Run

```bash
npm install                # once
npm run test:chromium      # or test:firefox / test:webkit
npm run test:all           # all three engines sequentially, combined summary
```

`components.js` is copied fresh from `src/Lumeo/wwwroot/js/components.js` at
the start of every run — never edit the copy in this directory, it is
git-ignored and gets overwritten on the next run.

## Adding an engine-specific fixture

`node run.js <chromium|firefox|webkit>` (also reads `PW_ENGINE`). Missing
engines: `npx playwright-core install firefox webkit` (Chromium ships with
most toolchains already).

## Cross-engine test isolation

Tests build their fixture by replacing `#host`'s `innerHTML`, but several
`register*` interop calls are never paired with `unregister*` (intentional —
some tests specifically probe cross-grid coexistence). One instance of the
resulting shared page state — TEST13's mouse-pointerId-reuse scenario —
was found to corrupt TEST14's result on WebKit only (Chromium/Firefox
unaffected); a targeted `page.reload()` between those two tests resets
WebKit's own gesture-recognizer state without touching product code or
loosening any assertion. See the comment at that call site in `run.js` if a
similar cross-engine-only failure shows up elsewhere in the suite — bisect
by skipping test bodies before assuming a product bug.
