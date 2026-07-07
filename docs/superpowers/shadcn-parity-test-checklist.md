# shadcn Parity — Device-Test Checklist

Manual acceptance pass for the five shadcn/Radix parity waves on branch
`feat/shadcn-parity-wave1`. Written for a human tester on **real devices**:
run every section twice on desktop (mouse + keyboard) and once on a phone
(touch), and toggle **light and dark** at least once per wave. A full pass is
~30–45 minutes.

Legend: 🖥️ desktop/keyboard only · 📱 phone/touch · 🌗 do in both themes.

---

## 1. Setup

- [ ] Check out `feat/shadcn-parity-wave1` (or install the first `4.1.0-preview.*`
      cut **after** preview.15 — these waves are unreleased as of preview.15).
- [ ] Run the docs app (Blazor **WASM**): `dotnet run` in `docs/Lumeo.Docs`,
      open `http://localhost:5287` (HTTPS: `https://localhost:7009`).
- [ ] Component demos live at `/components/<name>`. Routes used below:
      accordion, collapsible, checkbox, switch, toggle, toggle-group, slider,
      progress, sidebar, dropdown-menu, menubar, context-menu, navigation-menu,
      tooltip, hover-card, chart, agent-message-list.
- [ ] Have DevTools open (Elements + Console). Several checks read a
      `data-*` attribute or watch for a zero page-error console.
- [ ] Toggle the theme with the header light/dark switch; retest the 🌗 items.

---

## 2. Wave 1 — Overlay exit animations (menu-style overlays)

Guards: symmetric open/close motion + `data-state="closed"` on Dropdown,
Menubar, Tooltip, HoverCard, NavigationMenu (regressions where menus vanished
instantly on close).

- [ ] 🖥️🌗 `/components/dropdown-menu` — open a menu, press **Escape**. Expect:
      panel **zooms + fades out over ~150 ms** (not an instant disappear);
      in Elements the content briefly shows `data-state="closed"` before removal.
- [ ] 🖥️ `/components/menubar` — open a top menu, click a different top item.
      Expect the first menu animates out while the second animates in (no flicker).
- [ ] 🖥️ `/components/tooltip` — hover a trigger, then move away. Tooltip fades
      out smoothly; it does not linger or pop.
- [ ] 🖥️ `/components/hover-card` — hover then unhover. Card fades/zooms out.
- [ ] 🖥️ `/components/navigation-menu` — open a menu, move to another top item,
      then off the bar entirely. Content transitions out; no frozen panel.

---

## 3. Wave 2 — data-* state hooks (styling contract)

Guards: Radix-placement `data-state` / `data-disabled` / `data-orientation` so
consumer CSS (`data-[state=...]`, `group-data-[collapsible=icon]:`) keeps working.

- [ ] 🖥️ `/components/accordion` — open an item; in Elements confirm the trigger
      button + content carry `data-state="open"` and the root has
      `data-orientation="vertical"`. Close it → `data-state="closed"`.
- [ ] `/components/collapsible` — toggle open/closed; content shows
      `data-state` flipping. Set a **disabled** demo (if present): trigger has
      `aria-disabled`, `tabindex="-1"`, and clicking does nothing.
- [ ] `/components/checkbox` — tick, untick, and check an indeterminate demo;
      the button shows `data-state=checked|unchecked|indeterminate`.
- [ ] `/components/switch` — toggle; button **and** thumb span both show
      `data-state=checked|unchecked`.
- [ ] `/components/toggle` — press; `data-state` flips `on`/`off`.
- [ ] `/components/toggle-group` — group has `data-orientation` + `aria-orientation`;
      each item exposes `data-state=on|off`.
- [ ] `/components/slider` — drag; root/track/range/thumb carry `data-orientation`;
      a disabled demo carries `data-disabled` and won't drag.
- [ ] `/components/progress` — verify a progressbar reads `data-value`/`data-max`
      and `data-state` transitions loading → complete at 100% (and
      `indeterminate` on the indeterminate demo).
- [ ] 🖥️📱 `/components/sidebar` — collapse to icon rail; the `<aside>` shows
      `data-collapsible="icon"` (Push/Overlay/mobile collapse → `offcanvas`),
      plus `data-state`, `data-variant`, `data-side`, and the `group` class.

---

## 4. Wave 3 — native form participation

Guards: Checkbox + Switch post their value in a real `<form>` via a hidden
Radix-style bubble input; only when `Name` is set.

- [ ] 🖥️ `/components/checkbox` — on a demo with `Name` set, inspect the DOM for a
      visually-hidden `<input type="checkbox" aria-hidden tabindex="-1">` whose
      `checked` mirrors the visible state; toggling the box flips it.
      Indeterminate posts **unchecked**.
- [ ] 🖥️ `/components/switch` — same hidden input carries `Name`/`Value`
      (default `on`); it lives on the bubble input, **not** the `<button>`.
- [ ] 🖥️ If a "submit" demo exists, submit and confirm the checked field appears
      in the payload and an unchecked one does not (native form semantics).
- [ ] 📱 Tab order sanity: the hidden inputs are `tabindex="-1"` — keyboard focus
      lands on the visible control, never the bubble.

---

## 5. Wave 4 — menu-system interaction parity

Guards: Radix typeahead, ArrowLeft-closes-sub, destructive/inset variants,
right-aligned shortcuts, Menubar checkbox/radio, ContextMenu exit + NavMenu value.

- [ ] 🖥️ `/components/dropdown-menu` — open, **type the first letters** of an item
      (e.g. "se"); focus jumps to "Settings". A `Shortcut` renders **right-aligned**
      (8 px gap). A `destructive` item renders **red** (oklch). An `inset` item is
      indented. Open a submenu with **ArrowRight**, close with **ArrowLeft**
      (focus returns to the parent item).
- [ ] 🖥️ `/components/context-menu` — right-click the target. Repeat the typeahead,
      destructive, inset, ArrowLeft/Right checks. Then **Escape** → content
      **zooms out ~150 ms in place** (position persists through the exit, no jump).
- [ ] 🖥️ `/components/menubar` — a menu with a **CheckboxItem** and a
      **RadioGroup** binds correctly (check toggles; radio is single-select).
      Inset/Shortcut render like Dropdown.
- [ ] 🖥️ `/components/navigation-menu` — controlled demo: the active item reflects
      `Value`; hovering another and back within the grace window keeps it open.
      Content height is `auto` (no 0-height collapse — the old viewport-var bug).

---

## 6. Wave 5 — AI conversation + Chart accessibility

Guards: canvas-chart SR table, AgentMessage actions/branch nav, scroll-to-bottom.

- [ ] `/components/chart` — in Elements find a **visually-hidden `<table class="sr-only">`**
      with a caption like "Bar chart with 3 categories: Revenue" and `<th>` per
      category; the chart host is `tabindex="0"`, `role="img"` with an `aria-label`
      summary. (Turn a screen reader on for one chart if you can.)
- [ ] `/components/agent-message-list` — with a long thread, **scroll up**: a
      floating sticky **scroll-to-bottom** button appears; click it → jumps to the
      latest and the button hides.
- [ ] The **empty-state** demo renders its slot / localized default when there are
      no messages.
- [ ] AgentMessage **actions**: click **Copy** on a message → clipboard holds that
      message body and a "Copied" state shows; **Regenerate/Retry** fires its callback.
- [ ] AgentMessage **branch nav**: click **Next** → body switches (e.g. "First" →
      "Second answer") and the counter reads "2 of 3"; Prev/Loop behave.
- [ ] 🌗 Console stays **error-free** across all AgentMessageList/Chart interactions.

---

## 7. Regression — B10 / B11 service-overlay close paths

The service overlays (**Sheet / Dialog / Drawer** — the app's "service sheet"
lives here) had their exit infra reworked again for these waves. Re-run the
B10/B11 close-path matrix; these are the highest-value regression checks.

- [ ] 🖥️🌗 Open a **Sheet**, press **Escape**. Expect: panel **slides out** and the
      **backdrop fades in sync** (both finish together, ~300 ms) — the panel must
      **not freeze** mid-screen while the backdrop drops (the B11 bug).
- [ ] 🖥️ Open a Sheet, **click the backdrop** (outside the panel). It dismisses with
      the same synced slide+fade. (B10: the backdrop must actually catch the click —
      it previously had `pointer-events:none` and swallowed nothing.)
- [ ] 🖥️ Open a Sheet, click its **X / close button** and, separately, a custom
      in-content close button — both animate out identically to Escape/backdrop.
- [ ] 🖥️ **Double-Escape / spam Escape** on a closing Sheet: the second Escape is
      **inert** (does not re-enter the dismiss flow or cascade-close a parent
      overlay) — guards #185.
- [ ] 🖥️ **Dialog** and **Drawer**: repeat Escape + backdrop close; confirm synced
      exit and no residual backdrop left on screen after close.
- [ ] 📱 On a phone, open the service sheet and dismiss via backdrop tap and the
      close button; confirm the slide-out is smooth and nothing is left overlaying
      the page (no dead backdrop blocking taps).
- [ ] 🖥️ Nested case: open a Dropdown/Popover **inside** a Sheet, Escape once →
      only the inner menu closes; Escape again → the Sheet closes (no cascade).

---

## 8. Known out of scope (not yet built — do not file as bugs)

These shadcn primitives are **not implemented** in this branch; their absence is
expected. Skip and do not report:

- [ ] **Field** (form field wrapper / label+control+description composition)
- [ ] **Input Group** (addon/prefix/suffix input clusters)
- [ ] **Item** (generic list/row primitive)
- [ ] **Native Select** (styled native `<select>`)
- [ ] **Attachment** (chat/file attachment chip)
- [ ] **Marker** (map/annotation marker)
- [ ] **Bubble** (chat bubble primitive)

---

### Item count
Wave 1: 5 · Wave 2: 9 · Wave 3: 4 · Wave 4: 4 · Wave 5: 6 ·
B10/B11 regression: 8 · Out-of-scope: 7 — **43 items** across 8 sections.

---

## 9. Nachhärtung aus den Review-Runden 6–10 + Dialog-Animationen (NEU)

Diese Punkte kamen nach der ursprünglichen Liste dazu — Preview-Deploy nutzen oder lokal bauen.

**Dialog/AlertDialog/Drawer — Close-Animation jetzt Standard (der "Dialog ist instant"-Report):**
- [ ] 🌗 Dialog-Docs-Seite: deklarativen Dialog öffnen (zoomt/faded ein) und per X/Escape/Backdrop schließen — **Panel zoomt raus + Backdrop faded**, nichts poppt hart weg.
- [ ] AlertDialog + Drawer genauso (Drawer slidet ~300 ms).
- [ ] 📱 Dasselbe am Phone (Touch): Schließen animiert, kein Einfrieren.

**Modal-Backdrop schirmt während des Exits ab:**
- [ ] Dialog öffnen, dann **schnell doppelt** auf den Backdrop klicken/tippen: Der zweite Klick darf NICHTS auf der Seite dahinter auslösen (kein Button-Klick, keine Selektion "durch" den fadenden Backdrop).

**Exit-Fenster inert (Menü-Overlays):**
- [ ] Dropdown öffnen, dann **sehr schnell zweimal** auf denselben Menüpunkt klicken: Die Aktion feuert nur **einmal** (zweiter Klick trifft das fadende Menü nicht mehr).
- [ ] 🖥️ Dropdown schließen (Escape) und sofort Tab drücken: Der Fokus landet NICHT in dem noch faden­den Menü.
- [ ] HoverCard: Karte schließen lassen und den Mauszeiger auf die fadende Karte bewegen — sie darf NICHT wieder aufgehen.
- [ ] Dropdown mit offenem Submenu: Root schließen → Submenu verschwindet mit (kein Waisen-Panel).
- [ ] 🖥️ Tooltip am Viewport-Rand (geflippt): beim Ausfaden bleibt der Pfeil auf der geflippten Seite (springt nicht um).

**Sidebar-Shortcut + Editor-Schutz:**
- [ ] 🖥️ `Ctrl+B` (bzw. `Cmd+B`) irgendwo auf einer Seite mit Sidebar: Sidebar togglet.
- [ ] 🖥️ Cursor in ein Eingabefeld/Textarea setzen, `Ctrl+B` drücken: Sidebar togglet NICHT (Editor-Bold hat Vorrang).

**Form-Participation (neue Docs-Demos auf Checkbox/Switch-Seite):**
- [ ] Checkbox/Switch-Demo "Form Participation": Formular absenden → gesetzter Wert (Name/Value) erscheint im POST-Ergebnis der Demo; ungecheckt → kein Wert.

**Menü-Parität (neue Docs-Demos):**
- [ ] Menubar: Checkbox-Items haken ab, Radio-Items wechseln exklusiv, destructive Items sind rot, Shortcuts rechtsbündig.
- [ ] 🖥️ ContextMenu/Dropdown: Menü öffnen und Anfangsbuchstaben eines Items tippen → Fokus springt dorthin (Typeahead); im Submenu bleibt der Fokus im Submenu.
- [ ] ToggleGroup "Vertical"-Demo: Buttons stapeln sich vertikal.

**NavigationMenu (controlled, komplett überarbeitet):**
- [ ] Controlled-Demo auf der NavigationMenu-Seite: Panel folgt dem gebundenen Wert; 🖥️ ArrowDown auf einem Trigger öffnet UND fokussiert das Panel.

**Chart-Accessibility:**
- [ ] Chart-Seite, AccessibilityLayer-Demo: Toggle an/aus; mit DevTools: verstecktes `<table>` unterm Chart vorhanden; bei großen Serien max. ~50 Zeilen + "and N more data points"-Zeile.

**AgentMessage (AI-Seite):**
- [ ] Actions-Toolbar: Copy kopiert (Bestätigung erscheint), Regenerate/Retry-Callbacks feuern; Branch-Navigation (‹ ›) wechselt Nachrichten-Versionen.
- [ ] Message-Liste: hochscrollen → Scroll-to-latest-Button erscheint, Klick springt ans Ende; leere Liste zeigt den EmptyState.

## 10. Docs-Fakten-Spot-Check (NEU)

- [ ] Homepage: Hero/Stat/Footer sagen einheitlich **5,600+ tests** und **10 packages** (nicht mehr 5,331/4,983/7).
- [ ] Größentabelle: Charts-Zeile sagt "30+ chart types" (nicht "components"), DataGrid "3 components".
- [ ] 3–4 der neuen Demos stichprobenartig laden (Menubar, Checkbox-Form, Chart-A11y, NavigationMenu-Controlled): rendern ohne Fehler, Code-Beispiele passen zum Demo.
