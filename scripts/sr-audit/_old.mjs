import { chromium } from "playwright-core";
const b = await chromium.launch({ headless: false, args: ["--window-position=0,0","--window-size=1100,800"] });
const p = await b.newPage({ viewport: null });
await p.goto("http://localhost:5197/bewerben", { waitUntil: "load", timeout: 60000 });
await p.waitForSelector('input', { timeout: 60000 });
await new Promise(r=>setTimeout(r,3000));
await p.evaluate(() => document.documentElement.classList.add('dark'));  // wie im User-Screenshot
await new Promise(r=>setTimeout(r,600));
const ins = await p.$$('input');
// Genau das User-Szenario: rein ins Feld, raus ins naechste
for (let i = 0; i < Math.min(4, ins.length); i++) { await ins[i].click(); await new Promise(r=>setTimeout(r,400)); }
// etwas scrollen (der User-Screenshot war angescrollt), dann Fokus weg
await p.mouse.wheel(0, 120); await new Promise(r=>setTimeout(r,400));
await ins[4]?.click(); await new Promise(r=>setTimeout(r,400));
await p.mouse.wheel(0, 80); await new Promise(r=>setTimeout(r,1500));
console.log("DOM sagt (alle Inputs):", await p.evaluate(() => [...document.querySelectorAll('input')]
  .map(e => getComputedStyle(e).boxShadow === 'none' ? 'none' : 'RING').join(",")));
await new Promise(r=>setTimeout(r,14000));
await b.close();
