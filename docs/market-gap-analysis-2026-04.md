# Lumeo — Market Gap Analysis (April 2026)

Scope: benchmark Lumeo v1.7.0 (unreleased) against the top-8 Blazor UI libraries and identify what to add/improve to reach "best-in-market" status.

---

## 1. Lumeo inventory snapshot

**Package:** `Lumeo` — Blazor component library, .NET 10, Tailwind CSS v4, MIT (shadcn-inspired).

**Counts:**
- **103 UI components** under `src/Lumeo/UI/*`
- **8 color themes** (zinc, blue, green, rose, orange, violet, amber, teal) + dark mode
- **30+ chart types** via ECharts JS-interop wrapper
- **1,316 bUnit tests**
- **Runs on WASM + Server**

**Services (DI-registered):**
`ComponentInteropService`, `OverlayService` (programmatic dialogs/sheets/drawers/toasts), `ToastService`, `ThemeService`, `KeyboardShortcutService`, `ILumeoLocalizer` (EN/DE baseline).

**Interop modules:**
ClickOutside, FloatingPosition, Focus (+ trap), Resize, Scroll (+ lock), Swipe, Utility.

**Component inventory (condensed, by category):**

| Category | Components |
|---|---|
| Layout | Stack, Flex, Grid, Container, Center, Spacer, AspectRatio, Resizable, ScrollArea, Separator |
| Typography | Text, Heading, Link, Code |
| Forms | Input, Select, Combobox, DatePicker, DateTimePicker, TimePicker, NumberInput, PasswordInput, InputMask, Checkbox, Switch, RadioGroup, Slider, Toggle, ToggleGroup, FileUpload, OtpInput, TagInput, ColorPicker, Textarea, Form, Mention, Cascader, Rating, Segmented |
| Data display | Table, DataTable, DataGrid, Card, Badge, Chip, Avatar, Calendar, Descriptions, Statistic, Timeline, Steps, Image, ImageCompare, TreeView, TreeSelect, QRCode, Watermark, InplaceEditor, Filter, List |
| Feedback | Toast, Alert, Progress, Spinner, Skeleton, EmptyState, Result |
| Overlay | Dialog, Sheet, Drawer, AlertDialog, Popover, Tooltip, HoverCard, ContextMenu, DropdownMenu, Command, PopConfirm, Tour, Overlay |
| Navigation | Tabs, Breadcrumb, Pagination, Sidebar, Menubar, NavigationMenu, MegaMenu, Accordion, Collapsible, Scrollspy, BackToTop, Affix, SpeedDial, BottomNav |
| Drag & drop | Kanban, Sortable, Transfer |
| Charts | Chart (ECharts wrapper) — 30+ types |
| Utility | Icon, Kbd, Label, Text etc. |

**Distinctive for a free/MIT lib:** programmatic overlay API, ILumeoLocalizer, 8 themes, ImageCompare, Watermark, QRCode, Cascader, Mention, InplaceEditor, ImageMask, Tour, MegaMenu, Kanban, ColorPicker with HSV+Hue.

---

## 2. Competitor capability matrix

Columns: **Lum** = Lumeo, **Mud** = MudBlazor, **Rad** = Radzen, **Ant** = AntDesign Blazor, **Syn** = Syncfusion, **Tel** = Telerik, **Flu** = FluentUI, **Bls** = Blazorise, **Dex** = DevExpress. `✓` = ships, `~` = partial/basic, `✗` = missing.

| Capability | Lum | Mud | Rad | Ant | Syn | Tel | Flu | Bls | Dex |
|---|---|---|---|---|---|---|---|---|---|
| **DataGrid** (sort/filter/edit/virtualization) | ✓ | ✓ | ✓ | ~ | ✓ | ✓ | ✓ | ✓ | ✓ |
| DataGrid: grouping, pivot, aggregates | ~ | ~ | ✓ | ✗ | ✓ | ✓ | ~ | ~ | ✓ |
| DataGrid: Excel/PDF/CSV export | ~ (CSV/JSON) | ✗ | ✓ | ~ | ✓ | ✓ | ✗ | ~ | ✓ |
| **PivotGrid / PivotTable** | ✗ | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Scheduler / Event calendar** | ✗ | ~ (3rd party) | ✓ | ~ (Calendar) | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Gantt** | ✗ | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Rich Text Editor / HTML editor** | ✗ | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Spreadsheet (Excel-like)** | ✗ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ (RichEdit) |
| **Query Builder** | ✗ | ✗ | ~ (Filter) | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **File Manager / Browser** | ✗ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ | ~ |
| **Diagram / Flowchart** | ✗ | ✗ | ✓ (Sankey) | ✗ | ✓ | ✗ | ✗ | ✗ | ~ |
| **Kanban** | ✓ | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| **Charts** (30+ types) | ✓ (ECharts) | ~ (basic) | ✓ (~20) | ✗ | ✓ (55+) | ✓ (~60) | ~ | ~ | ✓ (~40) |
| **GeoMap** | ✓ (ECharts) | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Gauge** | ✓ (ECharts) | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **TreeGrid / TreeList** | ~ (TreeView) | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Signature pad** | ✗ | ✗ | ~ (HtmlEditor) | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| **Splitter panes** | ~ (Resizable) | ✗ | ✓ | ✗ | ✓ | ✓ | ✗ | ~ | ✓ |
| **DockManager** | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| **ReportViewer (SSRS/Telerik Rpt)** | ✗ | ✗ | ✓ (SSRS) | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **PDF Viewer** | ✗ | ✗ | ✗ | ✗ | ✓ | ~ | ✗ | ✗ | ✗ |
| **Word Processor / DocX** | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✓ |
| **Speech / AI-assist widgets** | ✗ | ✗ | ~ | ✗ | ✓ (AI-smart) | ✓ (2026) | ✗ | ✗ | ~ |
| **ListBox / DualListBox / PickList** | ~ (Transfer) | ✗ | ✓ | ✓ (Transfer) | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Mention / @-input** | ✓ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Cascader** | ✓ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Carousel** | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ |
| **ImageCompare** | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **QRCode** | ✓ | ✗ | ✗ | ✗ | ✓ (Barcode) | ✗ | ✗ | ✗ | ✗ |
| **Tour / OnboardingWalkthrough** | ✓ | ✗ | ✗ | ✓ | ~ | ✗ | ✗ | ✗ | ✗ |
| **Watermark** | ✓ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **MaskedInput** | ✓ (InputMask) | ✓ | ✓ (Mask) | ✗ | ✓ | ✓ | ✗ | ✓ | ✓ |
| **OTP input** | ✓ | ✗ | ✓ (SecurityCode) | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ |
| **Color picker (HSV wheel)** | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✗ | ✓ | ✓ |
| **Programmatic overlay API** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Localization (built-in resources)** | ~ (EN/DE) | ~ | ✓ | ✓ | ✓ (20+) | ✓ (40+) | ✓ | ~ | ✓ (30+) |
| **RTL support** | ✗ | ~ | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | ✓ |
| **Accessibility (WCAG 2.2 AA)** | ~ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | ✓ |
| **Theme designer (visual)** | ~ (customizer page) | ✗ | ✓ (Studio) | ✗ | ✓ | ✓ (ThemeBuilder) | ~ | ✓ | ✓ |
| **Figma design kit** | ✗ | ✗ | ~ | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ |
| **VS / Rider item templates** | ✗ | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Source generators / compile-time** | ✗ | ~ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Docs site polish** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | ✓ |
| **Price** | free/MIT | free/MIT | free/MIT | free/MIT | paid | paid | free | free+paid | paid |
| **Component count (vendor claim)** | 103 | ~90 | 110+ | 65 | 145+ | 120+ | ~75 | ~57 | 70+ |

---

## 3. Gaps — components Lumeo is missing

Priority = frequency a typical Blazor dev needs it. H / M / L.

### High priority (business-app table stakes)

| Component | Why | Competitors that ship | Effort |
|---|---|---|---|
| **Scheduler / EventCalendar** | LOB apps need booking/agenda UIs. No serious Blazor kit lacks it except Lumeo, Mud, Flu. | Rad, Syn, Tel, Dex | L |
| **RichTextEditor (WYSIWYG)** | CMS/email/notes. Wrap TipTap/Quill/Editor.js via JS-interop. | Rad, Syn, Tel, Dex | M |
| **TreeGrid / TreeList** | Hierarchical data (org, BOM, files). Today you force Table + TreeView combo. | Rad, Syn, Tel, Dex | M |
| **PivotGrid** | Analytics dashboards. Skip deep mode but ship a light client-side one. | Rad, Syn, Tel, Dex | L |
| **Gantt** | Project mgmt, resource planning. | Rad, Syn, Tel, Dex | L |
| **Query Builder** | Filter-by-expression UIs on top of grids. | Syn, Tel, Dex | M |
| **FileManager / Browser** | S3/Blob/disk explorers. | Syn, Tel | M |
| **Splitter** (multi-pane) | IDE/dashboard layouts. `Resizable` covers 2-pane only. | Rad, Syn, Tel, Dex, Bls | S |
| **PickList / DualListBox** | Admin screens (permissions, tag assignment). `Transfer` is close but rename/extend. | Rad, Syn, Tel, Ant, Dex | S |
| **MaskedInput variants** (phone/credit-card/IBAN presets) | Reduce boilerplate. | Syn, Tel, Dex | S |

### Medium priority

| Component | Why | Effort |
|---|---|---|
| Signature pad | Contracts/forms. Simple canvas wrap. | S |
| Diagram / FlowchartBuilder | Low-code, workflow tools. | L |
| OrgChart | HR, asset hierarchies. | M |
| DockManager | IDE-style layouts. Only Syncfusion ships real one. | L |
| Wizard / MultiStepForm | Prescribe Steps + Form composition. Today ad-hoc. | S |
| Spreadsheet (lite) | Editable grid with formulas; wrap Handsontable/Luckysheet. | L |
| PDF Viewer | Wrap pdf.js. | M |
| Knob | Rare but cheap; IoT dashboards. | S |
| TimeSpanPicker / DurationPicker | Scheduler companion. | S |
| CronExpressionBuilder | Job schedulers, cron UIs. | M |
| BarcodeScanner (camera) | Mobile-first apps. | M |
| SpeedDial — already present, but add FAB+Tooltip cluster recipes | | S |
| ImageEditor (crop/rotate) | Asset uploads. | M |
| DropzoneUpload enhancements (chunking, resume, progress per file) | Today FileUpload is basic. | M |

### Low priority

| Component | Why | Effort |
|---|---|---|
| ReportViewer (SSRS) | Niche; only Radzen ships free. Skip unless you find demand. | L |
| Word Processor | Moat for Syncfusion/DevExpress. Don't compete. | XL |
| ThemeRiver / specialty charts | Already covered via ECharts. | — |
| AI-smart grid (anomaly/semantic search) | Differentiator but need LLM plumbing decisions first. | L |
| SpeechToTextButton | Nice accent; commodity via Web Speech API. | S |

---

## 4. Gaps — Features / DX / Tooling

### Must-have DX

| Gap | What to build | Competitor parity |
|---|---|---|
| **DataGrid virtualization** | Verify/expose row + column virtualization on `DataGrid`. Mud, Syn, Tel, Ignite all have it. Benchmark 100k rows. | table-stakes |
| **Excel/PDF export (DataGrid + reports)** | Use ClosedXML (Excel), QuestPDF (PDF). Offer `DataGridExport` helper. | Rad, Tel, Syn, Dex |
| **Print / printable reports** | `<Lumeo.Print>` component + CSS `@media print` presets + `IPrintService`. | Tel |
| **RTL support** | `dir="rtl"` propagation; flip tokens in `lumeo.css`; test on Drawer/Sheet/Carousel/Stepper. | Ant, Rad, Syn, Tel, Flu |
| **Localization coverage** | Today EN/DE. Ship 12+ locales (ES, FR, IT, PT, NL, PL, JA, ZH-CN, KO, AR, RU, TR). Publish contributor guide. | Syn, Tel, Flu |
| **Visual Studio / Rider / dotnet new item templates** | `dotnet new lumeo-page`, `lumeo-form` scaffolders. | Mud, Syn, Tel, Flu |
| **Figma design kit** | Publish a community Figma library matching the 8 themes. | Syn, Tel, Dex, Flu |
| **Accessibility audit + WCAG 2.2 AA badge** | Automated axe-core pass in CI; fix outliers (Calendar, DataGrid, Kanban). | Mud, Rad, Flu |
| **Source generators** | `[LumeoForm]` on a POCO → generates a `<Form>` with all fields + validation bound. Would be a genuine differentiator. | nobody |
| **AI codegen snippets (MCP + schema)** | Publish an MCP server / JSON schema of all components so ChatGPT/Claude/Copilot generate idiomatic Lumeo markup. | Syn (AI-smart), Tel (unified search AI prompt) |
| **Schema-driven forms** | `<Form Schema="@myJsonSchema" />` renderer w/ conditional fields. Blazor Blueprint does this. | Blazor Blueprint |
| **SignalR real-time helpers** | `LumeoPresence`, `LumeoLiveCursor`, `LumeoSharedSelection` primitives on a hub. Hot 2026 trend. | none |
| **ColumnChooser, StateManagement (persist grid state)** | Save/restore grid col order, filters, sorts per user. | Syn, Tel, Dex |
| **DataGrid ODataDataSource / GraphQL helpers** | Plug-and-play server-driven data. | Rad (HasManyToMany, OData), Syn |
| **Offline docs / downloadable samples** | `lumeo.nativ.sh` plus `dotnet new lumeo-samples`. | Mud, Tel |

### Nice-to-have

- Tailwind v4 plugin that emits `@lumeo/...` utilities so devs don't need `lumeo-utilities.css`
- Storybook/Ladle-style component playground with props panel
- `lumeo-cli` — `lumeo add datagrid` copies/vendorizes component source (shadcn-style)
- Hot-reload friendly theme switcher
- Per-component bundle size badges
- Published benchmark suite vs. Mud/Radzen

---

## 5. Top 10 recommendations — ranked

Goal: move Lumeo from "solid free library" to "best-in-market free Blazor UI."

| # | Recommendation | Rationale (one-liner) | Effort | Table-stakes from |
|---|---|---|---|---|
| 1 | **Scheduler / EventCalendar component** | Biggest single hole vs. every paid lib + Radzen. Business apps need it. | L | Rad, Syn, Tel, Dex |
| 2 | **RichTextEditor** | Wrap TipTap/Quill. Covers CMS, notes, email-compose. | M | Rad, Syn, Tel, Dex |
| 3 | **TreeGrid + Splitter + PickList** | Three small components that unlock admin/LOB UIs immediately. | S+M | Rad, Syn, Tel |
| 4 | **Excel/PDF/Print export + DataGrid state persistence** | Most-requested enterprise DX feature set. ClosedXML + QuestPDF are free. | M | Rad, Syn, Tel, Dex |
| 5 | **Schema-driven forms + source generator** | `[LumeoForm]` on a POCO auto-renders form with validation. No competitor has it — become the differentiator. | M | none (moat) |
| 6 | **Full localization (12+ locales) + RTL** | Cheapest upgrade for global reach; competitor parity. | S | Ant, Rad, Syn, Tel |
| 7 | **AI MCP server + schema** | Publish component schema so LLMs author Lumeo markup correctly. Viral for 2026 devs. | M | none |
| 8 | **Figma design kit + VS/Rider templates** | Designers adopt it; devs scaffold in seconds. | M | Syn, Tel, Dex, Flu |
| 9 | **Query Builder + OData/GraphQL DataSource** | Close the gap for "filter-as-expression" UIs. | M | Syn, Tel, Dex |
| 10 | **Gantt chart (via dhtmlxGantt or Frappe Gantt wrap)** | Fills last big component hole for PM apps. | L | Rad, Syn, Tel, Dex |

**Honorable mentions:** PivotGrid (lite), FileManager, Signature, Diagram, SignalR real-time helpers, Spreadsheet (lite).

---

## 6. What to deliberately NOT add

| Item | Why skip |
|---|---|
| **Word Processor / DocX editor** | Syncfusion + DevExpress have spent a decade here. Wrapping OnlyOffice is a maintenance bog. |
| **Deep PivotGrid (pivoting on 10M rows)** | Syncfusion's moat. Ship a *lite* client-side pivot if anything. |
| **Proprietary ReportViewer** | Telerik/DevExpress bundle their own report engines. Radzen's SSRS is enough. Skip unless users ask. |
| **Native mobile-only components (Xamarin/MAUI-flavored)** | Blazor Hybrid covers it. Don't fork. |
| **Commercial theme marketplace** | Complicates MIT story. Ship themes free. |
| **Yet-another chart engine** | ECharts is world-class; don't rewrite. Invest in *chart wrappers*, not a new core. |
| **Drag-drop whiteboard / Excalidraw** | Out of scope; Diagram covers flow needs. |
| **Your own Tailwind fork** | Stay on upstream v4. |
| **Voice/AI wrappers that presume a specific LLM** | Keep AI integrations schema-driven; don't pin to OpenAI/Anthropic. |
| **Heavy virtualization libraries for <10k row scenarios** | Built-in `Virtualize` is fine. Don't bikeshed. |

---

## 7. Signals we're watching (2026 trends)

- **AI-first components** — Syncfusion ships anomaly-detection DataGrid; Telerik is building unified AI-prompt/search. Expose a simple `IAiProvider` so users plug their LLM.
- **Schema-driven UI** — Blazor Blueprint and server-driven UIs are hot; source generators make C# a natural fit.
- **Real-time / collaborative** — SignalR presence/cursor primitives not yet packaged by any major Blazor lib.
- **WebGPU charts** — ECharts 6 / deck.gl route; defer until mainstream browsers normalize.
- **Source-vendoring (shadcn model)** — Lumeo is already shadcn-inspired; lean into it. Ship a `lumeo-cli` that vendorizes components so teams can fork + customize without forking the NuGet.
- **.NET 10 AOT / trimming** — Audit components for AOT-safe reflection; publish trimming annotations.

---

## 8. Sources

- https://mudblazor.com/components/datagrid
- https://github.com/MudBlazor/MudBlazor
- https://blazor.radzen.com/
- https://github.com/radzenhq/radzen-blazor
- https://antblazor.com/en-US/components/overview
- https://www.syncfusion.com/blazor-components
- https://www.telerik.com/blazor-ui/components
- https://www.telerik.com/blazor-ui/scheduler
- https://www.telerik.com/blazor-ui/gantt
- https://www.telerik.com/blazor-ui/spreadsheet
- https://www.fluentui-blazor.net/
- https://github.com/microsoft/fluentui-blazor
- https://blazorise.com/docs/components
- https://www.devexpress.com/blazor/
- https://docs.devexpress.com/Blazor/401113/components/pivot-grid
- https://community.devexpress.com/Blogs/aspnet/archive/2026/02/24/blazor-june-2026-roadmap-v26-1.aspx
- https://www.syncfusion.com/blogs/post/ai-powered-smart-blazor-components
- https://www.telerik.com/support/whats-new/blazor-ui/roadmap
- https://medium.com/@reenbit/emerging-trends-in-blazor-development-for-2026-70d6a52e3d2a
- https://blazorblueprintui.com/enterprise-blazor-components
- https://www.infragistics.com/products/ignite-ui-blazor

---

## React ecosystem comparison — shadcn & derivatives

### 9.1 Why this comparison matters

Lumeo was explicitly built in the shadcn aesthetic (Tailwind tokens, neutral palette, `border-border/40` restraint, Radix-style composition). The real bar for *look-and-feel* and *DX ergonomics* is set in the React world, not by MudBlazor or Radzen. Blazor libraries set the *breadth* bar (grids, schedulers). shadcn and its derivatives set the *polish, copy-paste DX, motion and AI-UI* bar. Lumeo needs to meet both.

### 9.2 Competitor matrix — React side

`✓` ships, `~` partial, `✗` missing. Columns: **Lum** = Lumeo, **shad** = shadcn/ui, **reui** = reui.io, **orig** = Origin UI (coss), **ace** = Aceternity, **magic** = Magic UI, **trem** = Tremor, **mant** = Mantine v9.

| Capability | Lum | shad | reui | orig | ace | magic | trem | mant |
|---|---|---|---|---|---|---|---|---|
| Core form controls (Input, Select, Combobox…) | ✓ | ✓ | ✓ | ✓ | ~ | ✗ | ~ | ✓ |
| DataGrid (sort/filter/virt) | ✓ | ~ (table) | ✓ (TanStack) | ~ | ✗ | ✗ | ~ | ✓ |
| Kanban / Sortable / Tree | ✓ | ✗ | ✓ | ~ | ✗ | ✗ | ✗ | ~ |
| Scheduler / Calendar events | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ (v9 @mantine/schedule) |
| Charts (breadth) | ✓ (30+) | ~ (recharts block) | ✓ (recharts) | ~ | ✗ | ✗ | ✓ (dash-first) | ✓ (Recharts 3) |
| Copy-paste / source vendor model | ✗ (NuGet) | ✓ (CLI registry) | ✓ (CLI + prefix) | ✓ | ✓ | ✓ | ~ | ✗ |
| Blocks / page templates | ~ (Patterns page) | ✓ (official blocks) | ✓ (1000+) | ✓ | ✓ (pro) | ✓ (landing) | ✓ (300 blocks) | ✓ (ui.mantine.dev) |
| Animated / motion primitives | ~ | ~ | ✗ | ✗ | ✓✓ (200+) | ✓✓ (150+) | ✗ | ~ (Marquee v9) |
| AI UI primitives (prompt/stream/tool-call) | ✗ | ✓ (AI Elements, ai-sdk) | ~ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Bento grid | ✗ | ✓ (block) | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ |
| Marquee / Orbit / Globe / Beam | ✗ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ | ~ |
| Number ticker / text-reveal / typing | ✗ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ |
| Dashboard blocks (small multiples, KPI cards) | ~ (ChartPatternDemos) | ✓ | ✓ | ~ | ✗ | ✗ | ✓✓ | ✓ |
| Tailwind v4 + `@theme` tokens | ✓ | ✓ | ✓ | ✓ | ~ | ✓ | ~ | ✗ (own engine) |
| Container queries default | ~ | ✓ | ✓ | ✓ | ~ | ~ | ✓ | ✗ |
| RTL support | ✗ | ✓ (Jan 2026) | ✓ | ✓ | ~ | ~ | ~ | ✓ |
| View transitions / route transitions | ✗ | ~ | ✗ | ✗ | ✓ | ✓ | ✗ | ~ |
| Registry / CLI vendor | ✗ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| Figma kit | ✗ | ✓ | ✓ | ✓ | ✓ (pro) | ~ | ✓ | ✓ |
| Primitive-swappable (Radix ↔ Base UI) | n/a | ✓ (Feb 2026) | ✓ (both) | ~ | ✗ | ~ | ✗ | n/a |

**Signal:** shadcn's Feb 2026 unification of Radix + Base UI + RTL + component composition + CLI v4 has moved the goal posts. reui.io is the closest "what could Lumeo become" reference — same shadcn design language, but adds 17 in-house primitives (DataGrid, Kanban, Tree, Stepper, Filters, Timeline, Sortable) that overlap exactly with Lumeo's strongest surface area.

### 9.3 Patterns Lumeo should adopt

**High priority (ship for 1.0 or 1.x):**

| Pattern | What it is | Why for Lumeo |
|---|---|---|
| **`lumeo-cli` vendorizer** | `lumeo add datagrid` drops the .razor/.cs + CSS into the consumer's repo (shadcn-style). Keep NuGet mode as default. | Directly matches shadcn distribution philosophy. Lets teams fork/customize without PR-ing upstream. No Blazor competitor has it. |
| **Block templates** | Dashboards, auth (sign-in/sign-up/reset/OTP), pricing tables, hero/marketing sections, settings pages. | Lumeo's Patterns page is inline demos, not copy-pasteable composed blocks. reui/shadcn ship 1000+. Even 30 well-chosen Razor blocks would close the gap. |
| **AI UI primitives** | `<PromptInput>` (auto-grow, token counter slot, attach/send), `<StreamingText>` (SignalR chunk renderer), `<AgentMessageList>`, `<ToolCallCard>`, `<ReasoningDisplay>`. | shadcn has AI Elements + Vercel AI SDK; no Blazor equivalent. SignalR streaming is Lumeo's natural advantage — ship this as a differentiator, not a follower. |
| **Motion primitives** | `<Marquee>`, `<NumberTicker>`, `<TextReveal>`, `<BlurFade>`, `<BorderBeam>`, `<ShimmerButton>`, `<Sparkles>`, `<AnimatedBeam>`. | Landing/marketing + dashboard KPI tickers. Cheap CSS-first implementations (no JS for most). Mantine v9 just added Marquee — signal that mainstream libs are adopting this. |
| **Bento grid + container-query-aware tiles** | `<Bento>` + `<BentoTile>` with CQ breakpoints; tiles reflow based on their own box, not viewport. | Signature 2026 layout pattern. Lumeo's `Grid` is viewport-responsive only. |

**Medium priority:**

| Pattern | What |
|---|---|
| **Registry JSON schema** | Publish `registry.json` of every Lumeo component + block (same schema shadcn uses) so MCP/LLM tools can author Lumeo markup. |
| **View transitions wrapper** | `<LumeoTransition Name="...">` using the View Transitions API for route/tab changes. |
| **Spring / motion presets** | Named easings (`ease.snappy`, `ease.soft`) that map to both CSS timing and any JS animation the user opts in to. |
| **Typography components** | `<Prose>`, `<Display>`, `<Lead>` — Tailwind Typography-like but themed. |
| **Dashboard blocks (Tremor-style)** | `<KpiCard>`, `<SparkCard>`, `<SmallMultiple>`, `<Delta>` — data-viz shorthand above ECharts. |
| **Pricing table / hero / CTA blocks** | 5–8 composed blocks for marketing Razor pages. |

**Low priority:**
- Spotlight/3D-tilt cards (Aceternity territory — showy, but narrow use in LOB apps Lumeo targets)
- Globe/Orbit/Icon-cloud (cool, niche)
- Smooth cursor / confetti / cool-mode (gimmicks)

### 9.4 Differentiation opportunities — where Blazor wins

Honest list. These are things the React ecosystem *structurally cannot match* because of language/runtime.

| Opportunity | Why Blazor wins |
|---|---|
| **SignalR-native AI streaming** | React needs Vercel AI SDK + a server. Blazor Server has a persistent SignalR pipe already — `<StreamingText>` is 20 lines. Makes token-by-token rendering trivial without SSE plumbing. |
| **C#-typed theme tokens** | shadcn uses CSS variables; there is no type safety. Lumeo can expose `LumeoTheme.Tokens.Primary` as a strongly-typed C# record bound to CSS vars — autocompletable + refactorable. |
| **`[Parameter]` type safety** | React prop types are lint-time at best. Lumeo params are compiler-checked. Lean into this in docs: "your `<DataGrid TItem="Order">` is type-checked; their TanStack table isn't." |
| **Source generators on POCOs** | `[LumeoForm]` on a C# class generates a fully-bound `<Form>` with validation. React ecosystem needs runtime zod + react-hook-form. |
| **Roslyn analyzers** | Ship analyzers that flag "missing `@attributes`", "raw hex color in Razor", "forgot `await` on `OverlayService.ShowAsync`". No React lib can do equivalent. |
| **WASM-native drag-drop** | Kanban/Sortable in Lumeo can run dnd logic in C# on the client without JS-interop hops. |
| **MCP server for LLM codegen** | Publish a Lumeo MCP server so Claude/ChatGPT/Copilot generate idiomatic Lumeo markup. Shadcn has registry JSON; going one step further with a real MCP gives Lumeo edge. |
| **Compile-time theme validation** | Unused theme tokens, missing translations (`ILumeoLocalizer`), undefined variants — all caught at build time, not runtime. |

### 9.5 Updated Top 10 (post-React analysis)

Replaces §5. Integrates the React findings — re-ranks based on "bar set by shadcn/reui/AI Elements" rather than "bar set by Radzen/Syncfusion."

| # | Recommendation | Rationale | Effort |
|---|---|---|---|
| 1 | **AI UI primitives + SignalR streaming** (`PromptInput`, `StreamingText`, `AgentMessageList`, `ToolCallCard`, `ReasoningDisplay`) | 2026 table stakes. shadcn has AI Elements; no Blazor lib does. Blazor Server SignalR makes this Lumeo's natural moat. | M |
| 2 | **`lumeo-cli` + registry.json + MCP server** | Ship shadcn-style vendoring + a registry schema + an MCP endpoint. Unlocks LLM codegen and team-fork workflows. | M |
| 3 | **Block templates (30+ composed Razor blocks)** | Dashboards, auth flows, pricing, hero, settings. Mirrors shadcn/reui distribution model. Patterns page becomes a *registry* of copy-paste blocks, not inline demos. | M |
| 4 | **Scheduler / EventCalendar** (Mantine v9 + Radzen + Syncfusion all ship) | Still the biggest single gap vs Blazor competitors. React peers now ship it too (Mantine 9 `@mantine/schedule`). | L |
| 5 | **Motion primitives** (`Marquee`, `NumberTicker`, `TextReveal`, `BlurFade`, `BorderBeam`, `ShimmerButton`, `AnimatedBeam`, `Sparkles`) | Closes the Aceternity/Magic UI aesthetic gap. Mostly CSS + small JS interop. Marketing pages + dashboard polish. | S-M |
| 6 | **Dashboard blocks (Tremor-style small-multiples, KPI cards, deltas)** | Tremor joined Vercel and is now fully free — bar for "dashboard-out-of-the-box" just rose. Add `KpiCard`, `SparkCard`, `Delta`, `SmallMultiple`. | S |
| 7 | **Bento grid + container-query layout** | Signature 2026 layout. Container queries let tiles self-adjust. Ship `<Bento>` + `<BentoTile>` with CQ-aware variants. | S |
| 8 | **RichTextEditor + TreeGrid + Splitter + PickList** (from original Top 10) | Enterprise Blazor table stakes still stand. | M |
| 9 | **Schema-driven forms + `[LumeoForm]` source generator** | Unique differentiator — no React lib can do it (structurally). Carry over from original Top 10. | M |
| 10 | **Excel/PDF/Print export + DataGrid state persistence + RTL + 12 locales** | Enterprise DX bundle. RTL now shipped by shadcn (Jan 2026) — mandatory parity. | M |

**Honorable mentions:** Figma kit, VS/Rider templates, view-transitions wrapper, typography components (`<Prose>`), Gantt, PivotGrid (lite), FileManager.

### 9.6 Additional sources (React analysis)

- https://ui.shadcn.com/docs/changelog
- https://ui.shadcn.com/docs/tailwind-v4
- https://ui.shadcn.com/docs/directory
- https://reui.io/
- https://reui.io/components
- https://github.com/keenthemes/reui
- https://originui.com/
- https://ui.aceternity.com/components
- https://magicui.design/docs/components
- https://www.tremor.so/
- https://vercel.com/blog/vercel-acquires-tremor
- https://park-ui.com/
- https://mantine.dev/changelog/9-0-0/
- https://chakra-ui.com/blog/announcing-v3
- https://elements.ai-sdk.dev/
- https://www.shadcn.io/ai
- https://ai-sdk.dev/docs/ai-sdk-ui
- https://vercel.com/blog/ai-sdk-6
- https://www.pkgpulse.com/blog/shadcn-ui-vs-base-ui-vs-radix-components-2026
- https://www.radix-ui.com/themes
- https://midrocket.com/en/guides/ui-design-trends-2026/
- https://desinance.com/design/bento-grid-web-design/
