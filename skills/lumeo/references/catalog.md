# Lumeo component catalog

All 155 components by category, plus 16 full-page patterns and the 58 theme tokens. Generated from `components-api.json` (`node skills/lumeo/gen-catalog.mjs`).

> This is the **offline fallback**. When the `lumeo-mcp` server is connected, prefer `lumeo_search` / `lumeo_get_component` / `lumeo_get_example` — they give the live, complete per-parameter API.

Satellite packages: a component tagged **[Charts]** needs `Lumeo.Charts`, **[DataGrid]** `Lumeo.DataGrid`, **[Editor]** `Lumeo.Editor`, **[Scheduler]** `Lumeo.Scheduler`, **[Gantt]** `Lumeo.Gantt`, **[Motion]** `Lumeo.Motion`. Everything else is in core `Lumeo`.

## AI

- **AgentMessageList** — Chat message stream for AI agents with role-based styling. _(sub-components: AgentMessage)_
- **PromptInput** — Multiline AI prompt textarea with submit + keyboard shortcuts.
- **ReasoningDisplay** — Collapsible chain-of-thought block for AI reasoning traces.
- **StreamingText** — Token-by-token streaming text renderer for AI responses.
- **ToolCallCard** — AI tool-invocation card showing call + result.

## Dashboard

- **Bento** — Masonry grid for dashboard tiles and marketing feature layouts. _(sub-components: BentoTile)_
- **Delta** — Trend indicator showing delta value with up/down arrow + color.
- **KpiCard** — Dashboard KPI tile showing value, label, and trend.
- **PickList** — Two-column shuttle picker — move items between lists.
- **SparkCard** — Small dashboard card with an inline sparkline chart.

## Data Display

- **Avatar** — Circular user image with initials fallback and status indicator. _(sub-components: AvatarFallback, AvatarGroup, AvatarImage)_
- **Badge** — Small label for counts, statuses, or category tags.
- **Barcode** — Inline SVG Code 128B barcode renderer (scannable).
- **Calendar** — Date picker calendar grid with single, range, and multi-select modes.
- **Card** — Flexible container with header, content, and footer slots. _(sub-components: CardContent, CardFooter, CardHeader)_
- **Chart** **[Charts]** — Declarative chart wrapper over ECharts — 30+ types supported. _(sub-components: AreaChart, BarChart, BoxPlotChart, CalendarHeatmapChart, CandlestickChart, ChartSkeleton, DonutChart, EffectScatterChart, FunnelChart, GaugeChart, GeoMapChart, GraphChart, HeatmapChart, LineChart, LiquidFillChart, MixedChart, NightingaleChart, ParallelChart, PictorialBarChart, PieChart, PolarBarChart, RadarChart, RadialChart, SankeyChart, ScatterChart, SunburstChart, ThemeRiverChart, TreeChart, TreemapChart, WaterfallChart, WordCloudChart)_
- **Chip** — Compact removable tag, optionally toggleable. _(sub-components: ChipGroup)_
- **DataGrid** **[DataGrid]** — Enterprise grid: sort, filter, inline edit, multi-level group (client + server), pin, virtualize, export. _(sub-components: DataGridBody, DataGridCell, DataGridColumnDef, DataGridColumnFilter, DataGridColumnVisibility, DataGridDetailRow, DataGridFooter, DataGridGroupRow, DataGridHeader, DataGridHeaderCell, DataGridPagination, DataGridRow, DataGridToolbar, DataGridToolbarColumns, DataGridToolbarCopySelected, DataGridToolbarExport, DataGridToolbarFullscreen, DataGridToolbarLayouts, ToolbarContent)_
- **DataTable** **[DataGrid]** — Table with sorting, pagination, and row selection built in. _(sub-components: DataTableSortableHeader)_
- **Descriptions** — Key-value pair list for read-only entity details. _(sub-components: DescriptionsItem)_
- **FileManager** — Headless file and folder explorer — folder tree, breadcrumb path, list/grid views, lazy loading, inline rename, context-menu operations.
- **FileViewer** — Universal file preview — auto-detects type from MIME / extension and renders PDF, images, video, audio, Markdown, JSON, CSV, source code (CodeMirror), and plain text inline; unknown types fall back to a download CTA. Pluggable per-kind renderer overrides; auth-aware HttpClient hook.
- **Filter** **[DataGrid]** — Composable faceted filter builder with chips. _(sub-components: FilterPill)_
- **Gantt** **[Gantt]** — Gantt component.
- **Gauge** — Single-value gauge with radial, arc, and linear variants and threshold colour bands.
- **Image** — Image with lazy-loading, loading skeleton, and error fallback. _(sub-components: ImageGallery)_
- **ImageCompare** — Before/after slider comparison for two images.
- **List** — Ordered/unordered list with Lumeo typographic styling. _(sub-components: ListItem)_
- **Map** **[Maps]** — Interactive geographic map powered by MapLibre GL — markers, polylines, polygons, circles, arcs, heatmaps, legend overlays, and popups; CARTO vector basemaps, no API key required. _(sub-components: MapArc, MapCircle, MapHeatmap, MapLegend, MapLegendItem, MapMarker, MapPolygon, MapPolyline, MapPopup)_
- **PdfViewer** **[PdfViewer]** — Inline PDF document viewer powered by pdf.js — page navigation, zoom controls, optional text search, and download.
- **PivotGrid** — Cross-tab / pivot table that summarizes flat data into rows x columns x aggregated measures.
- **QRCode** — Renders a QR code SVG for a string payload.
- **Scheduler** **[Scheduler]** — Calendar/agenda scheduler wrapping FullCalendar.
- **Sparkline** — Inline SVG trend chart primitive — line, area, or bars for tables and KPI strips.
- **Statistic** — Big-number statistic display with label and unit.
- **Steps** — Numbered step indicator for wizards and progress flows. _(sub-components: StepsItem)_
- **Table** — Minimal styled HTML table with header, row, cell components. _(sub-components: TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow)_
- **Timeline** — Vertical event timeline with icons and connectors. _(sub-components: TimelineItem)_
- **TreeView** — Hierarchical tree with expand/collapse and selection. _(sub-components: TreeViewNode)_
- **Watermark** — Repeating diagonal watermark overlay.

## Drag & Drop

- **Kanban** — Drag-and-drop board with swimlanes. _(sub-components: KanbanCard, KanbanColumn)_
- **Sortable** — Drag-and-drop reorderable list.
- **Transfer** — Dual-list transfer picker — left/right with arrows.

## Feedback

- **Alert** — Inline callout for status, warning, or informational messages.
- **EmptyState** — Illustrated placeholder for empty lists with call-to-action.
- **Progress** — Linear progress bar with determinate + indeterminate modes. _(sub-components: CircularProgress, StepsProgress)_
- **Result** — Full-page success/error/info status screen with actions.
- **RingProgress** — Circular determinate progress ring with optional centre label or custom content.
- **Skeleton** — Pulsing placeholder block for loading states. _(sub-components: SkeletonCard, SkeletonCircle, SkeletonText)_
- **Spinner** — Indeterminate loading spinner with size variants.
- **Toast** — Notification toast — renders from ToastService queue. _(sub-components: ToastAction, ToastClose, ToastDescription, ToastProvider, ToastTitle, ToastViewport)_

## Forms

- **Button** — Versatile button with variants, sizes, icons, and loading states.
- **Cascader** — Multi-level dropdown for hierarchical selection.
- **Checkbox** — Binary input with indeterminate state and accessible label.
- **CodeEditor** **[CodeEditor]** — Source-code editor wrapping CodeMirror 6 with on-demand language packs, dark/light/auto theming, and line numbers.
- **ColorPicker** — Hue + saturation/value picker with hex input.
- **Combobox** — Searchable select with filtering, custom values, and grouping. _(sub-components: ComboboxContent, ComboboxCreate, ComboboxEmpty, ComboboxInput, ComboboxItem)_
- **DatePicker** — Calendar popover for picking a single date or range. _(sub-components: DateRangePicker, DateWheelPicker)_
- **DateTimePicker** — Combined date + time picker with timezone awareness.
- **FileUpload** — Drag-and-drop file dropzone with progress and validation.
- **Form** — EditForm wrapper with styled validation, field groups, and submit state. _(sub-components: FormDescription, FormField, FormItem, FormLabel, FormMessage)_
- **InplaceEditor** — Click-to-edit text/number field that swaps in an input.
- **Input** — Styled text input with label, prefix/suffix, icons, error state.
- **InputMask** — Masked input for phone numbers, dates, and custom patterns.
- **Mention** — Textarea with @-trigger dropdown for mentioning users.
- **NumberInput** — Numeric input with stepper buttons and locale formatting.
- **OtpInput** — One-time password input, auto-advances between boxes.
- **PasswordInput** — Password field with show/hide toggle and strength meter.
- **QueryBuilder** — Visual AND/OR predicate-tree builder; serializes to JSON or a LINQ predicate. _(sub-components: QueryBuilderGroup)_
- **RadioGroup** — Grouped radio buttons with horizontal or vertical layout. _(sub-components: RadioGroupCard, RadioGroupItem)_
- **Rating** — Star rating input with half-star support.
- **RichTextEditor** **[Editor]** — WYSIWYG editor wrapping TipTap with Lumeo styling. _(sub-components: AiActionMenu, BubbleMenu, EditorToolbar, TriggerDropdown)_
- **Segmented** — Pill-shaped tab-like single-select control. _(sub-components: SegmentedItem)_
- **Select** — Native-feeling styled dropdown with search and groups. _(sub-components: SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger)_
- **Slider** — Range slider with single and dual thumb modes.
- **Switch** — Toggle switch for boolean settings.
- **TagInput** — Input that turns entries into removable tag chips.
- **Textarea** — Multiline text input with auto-resize option.
- **TimePicker** — Time-of-day picker with 12h/24h formats. _(sub-components: TimeWheelPicker)_
- **Toggle** — Two-state button with pressed/unpressed styling.
- **ToggleGroup** — Group of toggles with single or multiple selection. _(sub-components: ToggleGroupItem)_
- **TreeSelect** — Select input with a hierarchical tree dropdown.

## Layout

- **AspectRatio** — Constrains child content to a fixed width-to-height ratio.
- **Center** — Flexbox helper that centers its children on both axes.
- **Container** — Responsive max-width wrapper with consistent page padding.
- **Flex** — Flexbox wrapper exposing direction, gap, align, justify as props.
- **Grid** — CSS grid wrapper with columns + gap as props.
- **Resizable** — Draggable splitter for resizable panel layouts. _(sub-components: ResizablePanel, ResizablePanelGroup)_
- **ScrollArea** — Styled custom scrollbar container.
- **Separator** — Horizontal or vertical dividing rule.
- **Spacer** — Flex-grow spacer that pushes siblings apart.
- **Stack** — Vertical flex wrapper with gap prop.

## Motion

- **AnimatedBeam** **[Motion]** — SVG beam that traces an animated gradient path between two DOM elements.
- **BlurFade** **[Motion]** — Motion primitive: blur + fade-in on mount or when in view.
- **BorderBeam** **[Motion]** — Animated gradient border beam effect for hero elements.
- **Confetti** **[Motion]** — Burst of colored particles on demand via imperative Fire() method.
- **Dock** **[Motion]** — macOS-style icon dock with cursor-proximity magnification.
- **Marquee** **[Motion]** — Infinitely scrolling horizontal band of children.
- **NumberTicker** **[Motion]** — Animated count-up from zero to target number.
- **ShimmerButton** **[Motion]** — Button with animated shimmer border beam.
- **Sparkles** **[Motion]** — Decorative sparkle particle animation.
- **TextReveal** **[Motion]** — Word-by-word reveal animation on scroll.

## Navigation

- **Accordion** — Vertically stacked collapsible sections that expand to reveal content. _(sub-components: AccordionContent, AccordionItem, AccordionTrigger)_
- **Affix** — Pins an element to the viewport edge as the user scrolls.
- **AppBar** — Top application bar with start, center, and end slots; sticky and elevated variants.
- **BackToTop** — Floating button that scrolls the page back to the top.
- **BottomNav** — Mobile-first bottom navigation bar with icon items. _(sub-components: BottomNavFab, BottomNavItem)_
- **Breadcrumb** — Hierarchical page path with separator characters. _(sub-components: BreadcrumbEllipsis, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator)_
- **Carousel** — Slide-based content rotator with autoplay and keyboard nav. _(sub-components: CarouselContent, CarouselItem, CarouselNext, CarouselPrevious)_
- **Collapsible** — Single expandable region with animated height transition. _(sub-components: CollapsibleContent, CollapsibleTrigger)_
- **MegaMenu** — Full-width dropdown for site-wide navigation with columns. _(sub-components: MegaMenuGroup, MegaMenuItem, MegaMenuLink, MegaMenuPanel)_
- **Menubar** — Horizontal menubar with File/Edit-style dropdowns. _(sub-components: MenubarContent, MenubarItem, MenubarLabel, MenubarMenu, MenubarSeparator, MenubarShortcut, MenubarSub, MenubarSubContent, MenubarSubTrigger, MenubarTrigger)_
- **NavigationMenu** — Top-level site nav with animated dropdown panels. _(sub-components: NavigationMenuContent, NavigationMenuHamburger, NavigationMenuIndicator, NavigationMenuItem, NavigationMenuLink, NavigationMenuList, NavigationMenuMobile, NavigationMenuTrigger, NavigationMenuViewport)_
- **Pagination** — Page number bar with prev/next and configurable ranges. _(sub-components: PaginationContent, PaginationEllipsis, PaginationItem, PaginationNext, PaginationPrevious)_
- **Scrollspy** — Highlights the nav item matching the current scroll section. _(sub-components: ScrollspyLink, ScrollspySection)_
- **Sidebar** — Collapsible app sidebar with groups, menu, and trigger. _(sub-components: SidebarContent, SidebarFooter, SidebarGroup, SidebarGroupLabel, SidebarHeader, SidebarMenu, SidebarMenuButton, SidebarMenuItem, SidebarProvider, SidebarSeparator, SidebarTrigger)_
- **SpeedDial** — Floating action button that fans out sub-actions.
- **Splitter** — Resizable split pane for horizontal/vertical layouts. _(sub-components: SplitterDivider, SplitterPane)_
- **Stepper** — Stateful multi-step wizard with navigation, validation gating, and header indicators. _(sub-components: StepperStep)_
- **Tabs** — Tabbed content with keyboard nav and animated active indicator. _(sub-components: TabsContent, TabsList, TabsTrigger)_
- **Toolbar** — Horizontal toolbar container with separator, spacer, and group sub-components. _(sub-components: ToolbarGroup, ToolbarSeparator, ToolbarSpacer)_

## Overlay

- **AlertDialog** — Modal confirmation dialog that interrupts the user for destructive actions. _(sub-components: AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger)_
- **Command** — Command palette — keyboard-driven finder for actions. _(sub-components: CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator)_
- **ContextMenu** — Right-click menu tied to a container element. _(sub-components: ContextMenuCheckboxItem, ContextMenuContent, ContextMenuGroup, ContextMenuItem, ContextMenuLabel, ContextMenuRadioGroup, ContextMenuRadioItem, ContextMenuSeparator, ContextMenuSub, ContextMenuSubContent, ContextMenuSubTrigger, ContextMenuTrigger)_
- **Dialog** — Modal dialog with header, content, footer, and focus trap. _(sub-components: DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger)_
- **Drawer** — Slide-up sheet for mobile-first contextual content. _(sub-components: DrawerClose, DrawerContent, DrawerDescription, DrawerFooter, DrawerHeader, DrawerTitle, DrawerTrigger)_
- **DropdownMenu** — Menu button with items, separators, submenus, and checkboxes. _(sub-components: DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuGroup, DropdownMenuItem, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger, DropdownMenuTrigger)_
- **HoverCard** — Popover that opens on hover for rich previews. _(sub-components: HoverCardContent, HoverCardTrigger)_
- **Overlay** — Low-level backdrop primitive for custom popovers and modals.
- **PopConfirm** — Inline 'are you sure?' popover attached to a trigger.
- **Popover** — Positionable floating panel with anchor and arrow. _(sub-components: PopoverContent, PopoverTrigger)_
- **Sheet** — Slide-in side panel from left/right/top/bottom. _(sub-components: SheetClose, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle, SheetTrigger)_
- **Tooltip** — Hover/focus tooltip with arrow and configurable placement. _(sub-components: TooltipContent, TooltipTrigger)_
- **Tour** — Multi-step spotlight onboarding tour.
- **Window** — Non-modal draggable and resizable floating panel with minimize and maximize support.

## Typography

- **Code** — Inline or block monospace code snippet with optional copy button.
- **Heading** — Semantic h1-h6 heading with Lumeo typographic scale.
- **Highlighter** — Wraps occurrences of one or more search terms in the text with highlight marks.
- **Link** — Styled anchor with underline + color variants.
- **Text** — Paragraph text with size, color, weight props.

## Utility

- **AudioPlayer** — Audio Player component.
- **ButtonGroup** — Button Group component.
- **ConsentBanner** — Consent Banner component.
- **DropdownButton** — Dropdown Button component.
- **Icon** — Icon wrapper — renders Lucide icons via Blazicons.
- **Kbd** — Keyboard shortcut glyph — renders <kbd> with styling.
- **Label** — Form label that links to a control via for/id.
- **PullToRefresh** — Pull To Refresh component.
- **SafeArea** — Safe Area component.
- **SignaturePad** — Signature Pad component.
- **SplitButton** — Split Button component.
- **SwipeActions** — Swipe Actions component.
- **ThemeSwitcher** — Color-scheme picker that writes to ThemeService.
- **ThemeToggle** — Dark/light mode toggle button.
- **TouchRipple** — Touch Ripple component.

## Full-page patterns / blocks

Composed examples built entirely from Lumeo components. Get the full Razor source with `lumeo_get_pattern`.

- **Analytics** (`/blocks/analytics`) — A KPI analytics dashboard with metric overview cards, traffic source breakdown, page views trend, top pages table, device split bars, and geographic distribution.
- **Authentication** (`/blocks/authentication`) — Three production-ready auth screens: Sign In with social providers, Sign Up with password strength, and Two-Factor OTP verification.
- **Calendar / Scheduling** (`/blocks/calendar`) — A full-featured calendar view with event chips, category filters, a mini-calendar sidebar, upcoming list, and an inline event composer.
- **Chat** (`/blocks/chat`) — A full messaging interface with contact list, conversation view, time-grouped messages, and message input bar.
- **Dashboard** (`/blocks/dashboard`) — A complete analytics dashboard built with Lumeo components. Includes a top bar, stats cards, revenue chart, activity feed, data table, and mini charts.
- **E-Commerce** (`/blocks/ecommerce`) — An e-commerce admin dashboard with KPI cards, revenue chart, recent orders, top products, inventory alerts, and customer segment breakdown.
- **File Manager** (`/blocks/file-manager`) — A Dropbox-inspired file manager with workspace navigation, pinned folders, recent files grid, full file table, and contextual hover actions.
- **Filters** (`/blocks/filters`) — A full faceted-search product browser with a sticky filter sidebar, live category/brand/rating/price/date/stock filters, active chips, and a responsive product grid with favorites.
- **Multi-Step Form Wizard** (`/blocks/form-wizard`) — A polished four-step onboarding wizard with progress steps, real field inputs, preferences, and a review summary before submission.
- **Kanban Board** (`/blocks/kanban`) — A sprint board with polished task cards and native HTML5 drag-and-drop between columns.
- **Mail** (`/blocks/mail`) — A three-column inbox with folder rail, message list, reading pane, and reply composer.
- **Music Player** (`/blocks/music`) — A full-screen music application with sidebar library navigation, album grid browsing, a 'Made for You' row, and a persistent bottom playback bar with progress and volume controls.
- **Notifications** (`/blocks/notifications`) — A full notification center with tabbed inbox (All / Mentions / Activity / Followers / Security), time-grouped items with unread indicators, preference toggles, and live toast demos.
- **Settings Page** (`/blocks/settings`) — A full settings layout with sidebar navigation, profile, account security, billing, notifications, team management, and danger zone.
- **Social Feed** (`/blocks/social-feed`) — A three-column social media layout with navigation rail, timeline feed with posts and interactions, and a right sidebar with trending topics and follow suggestions.
- **Task Tracker** (`/blocks/task-tracker`) — A Linear-inspired task tracker with grouped status lanes, priority icons, label badges, subtask progress, multi-select with bulk actions, and a left navigation rail.

## Theme tokens

The ONLY legal colours. Use as Tailwind-style utilities: `bg-{token}`, `text-{token}`, `border-{token}`, `ring-{token}`, `fill-{token}`. Radius tokens → `rounded-[var(--radius-…)]`. Never raw hex/hsl; never `dark:` prefixes (dark mode swaps the variable values).

- `accent` → `--color-accent`
- `accent-foreground` → `--color-accent-foreground`
- `background` → `--color-background`
- `border` → `--color-border`
- `card` → `--color-card`
- `card-foreground` → `--color-card-foreground`
- `chart-1` → `--color-chart-1`
- `chart-2` → `--color-chart-2`
- `chart-3` → `--color-chart-3`
- `chart-4` → `--color-chart-4`
- `chart-5` → `--color-chart-5`
- `destructive` → `--color-destructive`
- `destructive-foreground` → `--color-destructive-foreground`
- `destructive-light` → `--color-destructive-light`
- `destructive-text` → `--color-destructive-text`
- `foreground` → `--color-foreground`
- `info` → `--color-info`
- `info-foreground` → `--color-info-foreground`
- `info-light` → `--color-info-light`
- `info-text` → `--color-info-text`
- `input` → `--color-input`
- `muted` → `--color-muted`
- `muted-foreground` → `--color-muted-foreground`
- `overlay-backdrop` → `--color-overlay-backdrop`
- `popover` → `--color-popover`
- `popover-foreground` → `--color-popover-foreground`
- `positive` → `--color-positive`
- `positive-foreground` → `--color-positive-foreground`
- `positive-light` → `--color-positive-light`
- `positive-text` → `--color-positive-text`
- `primary` → `--color-primary`
- `primary-foreground` → `--color-primary-foreground`
- `progress-stripe` → `--color-progress-stripe`
- `radius` → `--radius`
- `radius-lg` → `--radius-lg`
- `radius-md` → `--radius-md`
- `radius-sm` → `--radius-sm`
- `radius-xl` → `--radius-xl`
- `rating` → `--color-rating`
- `ring` → `--color-ring`
- `secondary` → `--color-secondary`
- `secondary-foreground` → `--color-secondary-foreground`
- `sidebar` → `--color-sidebar`
- `sidebar-accent` → `--color-sidebar-accent`
- `sidebar-accent-foreground` → `--color-sidebar-accent-foreground`
- `sidebar-border` → `--color-sidebar-border`
- `sidebar-foreground` → `--color-sidebar-foreground`
- `sidebar-primary` → `--color-sidebar-primary`
- `sidebar-primary-foreground` → `--color-sidebar-primary-foreground`
- `sidebar-ring` → `--color-sidebar-ring`
- `success` → `--color-success`
- `success-foreground` → `--color-success-foreground`
- `success-light` → `--color-success-light`
- `success-text` → `--color-success-text`
- `warning` → `--color-warning`
- `warning-foreground` → `--color-warning-foreground`
- `warning-light` → `--color-warning-light`
- `warning-text` → `--color-warning-text`
