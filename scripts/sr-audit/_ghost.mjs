import { chromium } from "playwright-core";
const b = await chromium.launch({ headless: false, args: ["--window-position=0,0","--window-size=700,760"] });
const p = await b.newPage({ viewport: null });
await p.goto("http://localhost:8903/index.html", { waitUntil: "load" });
await new Promise(r=>setTimeout(r,1200));
for (const id of ["#a1","#a2","#a3","#b1","#b2","#b3"]) { await p.click(id); await new Promise(r=>setTimeout(r,250)); }
await p.click("#c1");
await new Promise(r=>setTimeout(r,800));
console.log("DOM nach dem Verlassen:", await p.evaluate(() => [...document.querySelectorAll('.r15,.r2')]
  .map(e => e.id + "=" + getComputedStyle(e).boxShadow).join(" | ")));
await new Promise(r=>setTimeout(r,14000));
await b.close();
