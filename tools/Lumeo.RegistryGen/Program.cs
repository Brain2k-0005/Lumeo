using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lumeo.RegistryGen;

// Lumeo Registry Generator
// Scans src/Lumeo/UI/*/ and produces src/Lumeo/registry/registry.json
// Usage: dotnet run --project tools/Lumeo.RegistryGen [--lumeo-root <path>]

var repoRoot = FindRepoRoot(Environment.CurrentDirectory)
               ?? throw new InvalidOperationException("Could not locate Lumeo repo root (no Lumeo.slnx found).");

// Single source of truth: the lockstep <Version> in Directory.Build.props.
// Hardcoding it here is how registry.json/components-api.json drifted to a
// stale rc.NN label across releases — read it instead so they can't.
var lumeoVersion = ReadLockstepVersion(repoRoot);

// Scan core + every satellite package's UI directory. Discovery is automatic:
// any directory matching src/Lumeo*/UI is included, so adding a new satellite
// (e.g. Lumeo.FileViewer) doesn't need a hand-edit here. Before auto-discovery
// the list was hardcoded — when src/Lumeo.PdfViewer was added without
// updating this array, its component silently disappeared from the registry
// and `lumeo add pdf-viewer` returned 404 for two patch versions.
//
// Excludes: SourceGenerators (no UI), test/template/tooling projects.
var srcDir = Path.Combine(repoRoot, "src");
var uiRoots = Directory
    .EnumerateDirectories(srcDir, "Lumeo*", SearchOption.TopDirectoryOnly)
    .Where(d =>
    {
        var name = Path.GetFileName(d);
        if (string.Equals(name, "Lumeo.SourceGenerators", StringComparison.OrdinalIgnoreCase)) return false;
        return Directory.Exists(Path.Combine(d, "UI"));
    })
    .Select(d => Path.Combine(d, "UI"))
    // Stable ordering: core first, then satellites alphabetically. Keeps
    // registry diffs reviewable across runs.
    .OrderBy(p => Path.GetFileName(Path.GetDirectoryName(p)!) == "Lumeo" ? 0 : 1)
    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (uiRoots.Length == 0)
{
    Console.Error.WriteLine($"No UI roots discovered under {srcDir}.");
    return 1;
}
Console.WriteLine($"Discovered {uiRoots.Length} UI root(s): {string.Join(", ", uiRoots.Select(r => Path.GetRelativePath(repoRoot, r)))}");

var outputDir = Path.Combine(repoRoot, "src", "Lumeo", "registry");
var outputPath = Path.Combine(outputDir, "registry.json");

Directory.CreateDirectory(outputDir);

var coreUiRoot = uiRoots[0];
if (!Directory.Exists(coreUiRoot))
{
    Console.Error.WriteLine($"Core UI root not found: {coreUiRoot}");
    return 1;
}

// NuGet package map — components that live in satellite packages.
// Everything not listed here defaults to the "Lumeo" core package.
var componentToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    // Charts satellite
    ["Chart"] = "Lumeo.Charts",
    // DataGrid satellite
    ["DataGrid"] = "Lumeo.DataGrid",
    ["DataTable"] = "Lumeo.DataGrid",
    ["Filter"] = "Lumeo.DataGrid",
    // CodeEditor satellite
    ["CodeEditor"] = "Lumeo.CodeEditor",
    // Editor satellite
    ["RichTextEditor"] = "Lumeo.Editor",
    // Scheduler satellite
    ["Scheduler"] = "Lumeo.Scheduler",
    // Gantt satellite
    ["Gantt"] = "Lumeo.Gantt",
    // PdfViewer satellite
    ["PdfViewer"] = "Lumeo.PdfViewer",
    // Maps satellite
    ["Map"] = "Lumeo.Maps",
    ["MapMarker"] = "Lumeo.Maps",
    ["MapHeatmap"] = "Lumeo.Maps",
    ["MapLegend"] = "Lumeo.Maps",
    ["MapLegendItem"] = "Lumeo.Maps",
    ["MapPopup"] = "Lumeo.Maps",
    ["MapArc"] = "Lumeo.Maps",
    ["MapCircle"] = "Lumeo.Maps",
    ["MapPolygon"] = "Lumeo.Maps",
    ["MapPolyline"] = "Lumeo.Maps",
    // Motion satellite — Phase 1 (7 components)
    ["BlurFade"] = "Lumeo.Motion",
    ["BorderBeam"] = "Lumeo.Motion",
    ["Marquee"] = "Lumeo.Motion",
    ["NumberTicker"] = "Lumeo.Motion",
    ["ShimmerButton"] = "Lumeo.Motion",
    ["Sparkles"] = "Lumeo.Motion",
    ["TextReveal"] = "Lumeo.Motion",
    // Motion satellite — Phase 2 (10 Tier-1 components)
    ["AnimatedBeam"] = "Lumeo.Motion",
    ["AnimatedGradientText"] = "Lumeo.Motion",
    ["Confetti"] = "Lumeo.Motion",
    ["Dock"] = "Lumeo.Motion",
    ["Globe"] = "Lumeo.Motion",
    ["MagneticButton"] = "Lumeo.Motion",
    ["Meteors"] = "Lumeo.Motion",
    ["Ripple"] = "Lumeo.Motion",
    ["Spotlight"] = "Lumeo.Motion",
    ["TypingAnimation"] = "Lumeo.Motion",
    // Motion satellite — Phase 3 (13 Tier-2 components)
    ["OrbitingCircles"] = "Lumeo.Motion",
    ["AnimatedCircularProgressBar"] = "Lumeo.Motion",
    ["WordRotate"] = "Lumeo.Motion",
    ["RetroGrid"] = "Lumeo.Motion",
    ["AuroraBackground"] = "Lumeo.Motion",
    ["BackgroundBeams"] = "Lumeo.Motion",
    ["MorphingText"] = "Lumeo.Motion",
    ["AnimatedGridPattern"] = "Lumeo.Motion",
    ["ShineBorder"] = "Lumeo.Motion",
    ["MagicCard"] = "Lumeo.Motion",
    ["AnimatedSubscribeButton"] = "Lumeo.Motion",
    ["NumberCountUp"] = "Lumeo.Motion",
    ["HoverBorderGradient"] = "Lumeo.Motion",
};

// Category map derived from README.md structure.
// Keep in sync with README.md when adding components.
var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    // Layout
    ["Stack"] = "Layout",
    ["Flex"] = "Layout",
    ["Grid"] = "Layout",
    ["Container"] = "Layout",
    ["Center"] = "Layout",
    ["Spacer"] = "Layout",
    ["AspectRatio"] = "Layout",
    ["Resizable"] = "Layout",
    ["ScrollArea"] = "Layout",
    ["Separator"] = "Layout",
    // Typography
    ["Text"] = "Typography",
    ["Heading"] = "Typography",
    ["Link"] = "Typography",
    ["Code"] = "Typography",
    // Forms
    ["Input"] = "Forms",
    ["Select"] = "Forms",
    ["Combobox"] = "Forms",
    ["DatePicker"] = "Forms",
    ["DateTimePicker"] = "Forms",
    ["TimePicker"] = "Forms",
    ["NumberInput"] = "Forms",
    ["PasswordInput"] = "Forms",
    ["InputMask"] = "Forms",
    ["Checkbox"] = "Forms",
    ["Switch"] = "Forms",
    ["RadioGroup"] = "Forms",
    ["Slider"] = "Forms",
    ["Toggle"] = "Forms",
    ["ToggleGroup"] = "Forms",
    ["FileUpload"] = "Forms",
    ["OtpInput"] = "Forms",
    ["TagInput"] = "Forms",
    ["ColorPicker"] = "Forms",
    ["Textarea"] = "Forms",
    ["Form"] = "Forms",
    ["Mention"] = "Forms",
    ["Cascader"] = "Forms",
    ["Segmented"] = "Forms",
    ["Rating"] = "Forms",
    ["InplaceEditor"] = "Forms",
    ["RichTextEditor"] = "Forms",
    ["CodeEditor"] = "Forms",
    // Data Display
    ["Table"] = "Data Display",
    ["DataTable"] = "Data Display",
    ["DataGrid"] = "Data Display",
    ["PivotGrid"] = "Data Display",
    ["Card"] = "Data Display",
    ["Badge"] = "Data Display",
    ["Chip"] = "Data Display",
    ["Avatar"] = "Data Display",
    ["Calendar"] = "Data Display",
    ["Descriptions"] = "Data Display",
    ["Statistic"] = "Data Display",
    ["Timeline"] = "Data Display",
    ["Steps"] = "Data Display",
    ["Image"] = "Data Display",
    ["ImageCompare"] = "Data Display",
    ["TreeView"] = "Data Display",
    ["TreeSelect"] = "Forms",
    ["QRCode"] = "Data Display",
    ["Watermark"] = "Data Display",
    ["List"] = "Data Display",
    ["Scheduler"] = "Data Display",
    ["Sparkline"] = "Data Display",
    ["Gantt"] = "Data Display",
    ["Map"] = "Data Display",
    ["MapMarker"] = "Data Display",
    ["MapHeatmap"] = "Data Display",
    ["MapLegend"] = "Data Display",
    ["MapLegendItem"] = "Data Display",
    ["MapPopup"] = "Data Display",
    ["MapArc"] = "Data Display",
    ["MapCircle"] = "Data Display",
    ["MapPolygon"] = "Data Display",
    ["MapPolyline"] = "Data Display",
    // Feedback
    ["Toast"] = "Feedback",
    ["Alert"] = "Feedback",
    ["Progress"] = "Feedback",
    ["Spinner"] = "Feedback",
    ["Skeleton"] = "Feedback",
    ["EmptyState"] = "Feedback",
    ["Result"] = "Feedback",
    // Overlay
    ["Dialog"] = "Overlay",
    ["Sheet"] = "Overlay",
    ["Drawer"] = "Overlay",
    ["AlertDialog"] = "Overlay",
    ["Popover"] = "Overlay",
    ["Tooltip"] = "Overlay",
    ["HoverCard"] = "Overlay",
    ["ContextMenu"] = "Overlay",
    ["DropdownMenu"] = "Overlay",
    ["Command"] = "Overlay",
    ["PopConfirm"] = "Overlay",
    ["Tour"] = "Overlay",
    ["Overlay"] = "Overlay",
    // New components
    ["Stepper"] = "Navigation",
    ["Window"] = "Overlay",
    ["Toolbar"] = "Navigation",
    ["AppBar"] = "Navigation",
    ["Gauge"] = "Data Display",
    ["Barcode"] = "Data Display",
    ["RingProgress"] = "Feedback",
    ["Highlighter"] = "Typography",
    ["FileManager"] = "Data Display",
    ["PdfViewer"] = "Data Display",
    ["FileViewer"] = "Data Display",
    ["QueryBuilder"] = "Forms",
    // Navigation
    ["Tabs"] = "Navigation",
    ["Breadcrumb"] = "Navigation",
    ["Pagination"] = "Navigation",
    ["Sidebar"] = "Navigation",
    ["BottomNav"] = "Navigation",
    ["Menubar"] = "Navigation",
    ["NavigationMenu"] = "Navigation",
    ["MegaMenu"] = "Navigation",
    ["Accordion"] = "Navigation",
    ["Collapsible"] = "Navigation",
    ["Scrollspy"] = "Navigation",
    ["BackToTop"] = "Navigation",
    ["Affix"] = "Navigation",
    ["SpeedDial"] = "Navigation",
    ["Splitter"] = "Navigation",
    ["Carousel"] = "Navigation",
    // AI
    ["PromptInput"] = "AI",
    ["StreamingText"] = "AI",
    ["AgentMessageList"] = "AI",
    ["ToolCallCard"] = "AI",
    ["ReasoningDisplay"] = "AI",
    // Motion
    ["Marquee"] = "Motion",
    ["NumberTicker"] = "Motion",
    ["TextReveal"] = "Motion",
    ["BlurFade"] = "Motion",
    ["BorderBeam"] = "Motion",
    ["ShimmerButton"] = "Motion",
    ["Sparkles"] = "Motion",
    ["AnimatedBeam"] = "Motion",
    ["AnimatedGradientText"] = "Motion",
    ["Confetti"] = "Motion",
    ["Dock"] = "Motion",
    ["Globe"] = "Motion",
    ["MagneticButton"] = "Motion",
    ["Meteors"] = "Motion",
    ["Ripple"] = "Motion",
    ["Spotlight"] = "Motion",
    ["TypingAnimation"] = "Motion",
    // Phase 3 Tier-2
    ["OrbitingCircles"] = "Motion",
    ["AnimatedCircularProgressBar"] = "Motion",
    ["WordRotate"] = "Motion",
    ["RetroGrid"] = "Motion",
    ["AuroraBackground"] = "Motion",
    ["BackgroundBeams"] = "Motion",
    ["MorphingText"] = "Motion",
    ["AnimatedGridPattern"] = "Motion",
    ["ShineBorder"] = "Motion",
    ["MagicCard"] = "Motion",
    ["AnimatedSubscribeButton"] = "Motion",
    ["NumberCountUp"] = "Motion",
    ["HoverBorderGradient"] = "Motion",
    // Dashboard
    ["Bento"] = "Dashboard",
    ["KpiCard"] = "Dashboard",
    ["SparkCard"] = "Dashboard",
    ["Delta"] = "Dashboard",
    ["PickList"] = "Dashboard",
    // Drag & Drop
    ["Kanban"] = "Drag & Drop",
    ["Sortable"] = "Drag & Drop",
    ["Transfer"] = "Drag & Drop",
    ["Filter"] = "Data Display",
    // Charts (subgroup of Data Display per docs nav v2)
    ["Chart"] = "Data Display",
    // Utility
    ["Button"] = "Forms",
    ["Icon"] = "Utility",
    ["Kbd"] = "Utility",
    ["Label"] = "Utility",
    ["ThemeSwitcher"] = "Utility",
    ["ThemeToggle"] = "Utility",
    ["DensityScope"] = "Utility",
    // Forms / actions (3.3–3.5 additions)
    ["ConfirmButton"] = "Forms",
    ["UploadTrigger"] = "Forms",
    ["OverlayForm"] = "Forms",
    // Marketing / landing-page primitives (3.4 Splash Kit)
    ["Hero"] = "Marketing",
    ["FeatureGrid"] = "Marketing",
    ["FeatureItem"] = "Marketing",
    ["CTASection"] = "Marketing",
};

// One-line descriptions (hand-written-ish, name-based).
var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Stepper"] = "Stateful multi-step wizard with navigation, validation gating, and header indicators.",
    ["Window"] = "Non-modal draggable and resizable floating panel with minimize and maximize support.",
    ["Gauge"] = "Single-value gauge with radial, arc, and linear variants and threshold colour bands.",
    ["Barcode"] = "Inline SVG Code 128B barcode renderer (scannable).",
    ["RingProgress"] = "Circular determinate progress ring with optional centre label or custom content.",
    ["Highlighter"] = "Wraps occurrences of one or more search terms in the text with highlight marks.",
    ["FileManager"] = "Headless file and folder explorer — folder tree, breadcrumb path, list/grid views, lazy loading, inline rename, context-menu operations.",
    ["PdfViewer"] = "Inline PDF document viewer powered by pdf.js — page navigation, zoom controls, optional text search, and download.",
    ["FileViewer"] = "Universal file preview — auto-detects type from MIME / extension and renders PDF, images, video, audio, Markdown, JSON, CSV, source code (CodeMirror), and plain text inline; unknown types fall back to a download CTA. Pluggable per-kind renderer overrides; auth-aware HttpClient hook.",
    ["QueryBuilder"] = "Visual AND/OR predicate-tree builder; serializes to JSON or a LINQ predicate.",
    ["Toolbar"] = "Horizontal toolbar container with separator, spacer, and group sub-components.",
    ["AppBar"] = "Top application bar with start, center, and end slots; sticky and elevated variants.",
    ["Accordion"] = "Vertically stacked collapsible sections that expand to reveal content.",
    ["Affix"] = "Pins an element to the viewport edge as the user scrolls.",
    ["AgentMessageList"] = "Chat message stream for AI agents with role-based styling.",
    ["Alert"] = "Inline callout for status, warning, or informational messages.",
    ["AlertDialog"] = "Modal confirmation dialog that interrupts the user for destructive actions.",
    ["AspectRatio"] = "Constrains child content to a fixed width-to-height ratio.",
    ["Avatar"] = "Circular user image with initials fallback and status indicator.",
    ["BackToTop"] = "Floating button that scrolls the page back to the top.",
    ["Badge"] = "Small label for counts, statuses, or category tags.",
    ["Bento"] = "Masonry grid for dashboard tiles and marketing feature layouts.",
    ["AnimatedBeam"] = "SVG beam that traces an animated gradient path between two DOM elements.",
    ["AnimatedGradientText"] = "Text with a multi-stop gradient that shifts hue over time.",
    ["BlurFade"] = "Motion primitive: blur + fade-in on mount or when in view.",
    ["BorderBeam"] = "Animated gradient border beam effect for hero elements.",
    ["BottomNav"] = "Mobile-first bottom navigation bar with icon items.",
    ["Breadcrumb"] = "Hierarchical page path with separator characters.",
    ["Button"] = "Versatile button with variants, sizes, icons, and loading states.",
    ["Calendar"] = "Date picker calendar grid with single, range, and multi-select modes.",
    ["Card"] = "Flexible container with header, content, and footer slots.",
    ["Carousel"] = "Slide-based content rotator with autoplay and keyboard nav.",
    ["Cascader"] = "Multi-level dropdown for hierarchical selection.",
    ["Center"] = "Flexbox helper that centers its children on both axes.",
    ["Chart"] = "Declarative chart wrapper over ECharts — 30+ types supported.",
    ["Checkbox"] = "Binary input with indeterminate state and accessible label.",
    ["Chip"] = "Compact removable tag, optionally toggleable.",
    ["Code"] = "Inline or block monospace code snippet with optional copy button.",
    ["Collapsible"] = "Single expandable region with animated height transition.",
    ["ColorPicker"] = "Hue + saturation/value picker with hex input.",
    ["Combobox"] = "Searchable select with filtering, custom values, and grouping.",
    ["Command"] = "Command palette — keyboard-driven finder for actions.",
    ["Container"] = "Responsive max-width wrapper with consistent page padding.",
    ["ContextMenu"] = "Right-click menu tied to a container element.",
    ["DataGrid"] = "Enterprise grid: sort, filter, inline edit, multi-level group (client + server), pin, virtualize, export.",
    ["DataTable"] = "Table with sorting, pagination, and row selection built in.",
    ["DatePicker"] = "Calendar popover for picking a single date or range.",
    ["DateTimePicker"] = "Combined date + time picker with timezone awareness.",
    ["Delta"] = "Trend indicator showing delta value with up/down arrow + color.",
    ["Descriptions"] = "Key-value pair list for read-only entity details.",
    ["Dialog"] = "Modal dialog with header, content, footer, and focus trap.",
    ["Drawer"] = "Slide-up sheet for mobile-first contextual content.",
    ["DropdownMenu"] = "Menu button with items, separators, submenus, and checkboxes.",
    ["EmptyState"] = "Illustrated placeholder for empty lists with call-to-action.",
    ["FileUpload"] = "Drag-and-drop file dropzone with progress and validation.",
    ["Filter"] = "Composable faceted filter builder with chips.",
    ["Flex"] = "Flexbox wrapper exposing direction, gap, align, justify as props.",
    ["Form"] = "EditForm wrapper with styled validation, field groups, and submit state.",
    ["Grid"] = "CSS grid wrapper with columns + gap as props.",
    ["Heading"] = "Semantic h1-h6 heading with Lumeo typographic scale.",
    ["HoverCard"] = "Popover that opens on hover for rich previews.",
    ["Icon"] = "Icon wrapper — renders Lucide icons via Blazicons.",
    ["Image"] = "Image with lazy-loading, loading skeleton, and error fallback.",
    ["ImageCompare"] = "Before/after slider comparison for two images.",
    ["InplaceEditor"] = "Click-to-edit text/number field that swaps in an input.",
    ["Input"] = "Styled text input with label, prefix/suffix, icons, error state.",
    ["InputMask"] = "Masked input for phone numbers, dates, and custom patterns.",
    ["Kanban"] = "Drag-and-drop board with swimlanes.",
    ["Kbd"] = "Keyboard shortcut glyph — renders <kbd> with styling.",
    ["KpiCard"] = "Dashboard KPI tile showing value, label, and trend.",
    ["Label"] = "Form label that links to a control via for/id.",
    ["Link"] = "Styled anchor with underline + color variants.",
    ["List"] = "Ordered/unordered list with Lumeo typographic styling.",
    ["Map"] = "Interactive geographic map powered by MapLibre GL — markers, polylines, polygons, circles, arcs, heatmaps, legend overlays, and popups; CARTO vector basemaps, no API key required.",
    ["MapMarker"] = "Declarative marker child for Map: latitude, longitude, optional custom icon and rich popup content.",
    ["MapHeatmap"] = "Heatmap layer child for Map: renders a density overlay from a collection of (lat, lon, intensity) points.",
    ["MapLegend"] = "Legend overlay container for Map — shows a titled list of color-keyed labels inside the map viewport.",
    ["MapLegendItem"] = "Single color-swatch + label row inside a MapLegend.",
    ["MapPopup"] = "Stand-alone popup anchored to a geographic coordinate inside Map — toggled via IsOpen, independent of any marker.",
    ["MapArc"] = "Great-circle arc drawn between two coordinates on Map, with animated dash-draw effect.",
    ["MapCircle"] = "Filled circle layer on Map defined by center coordinates and radius in meters.",
    ["MapPolygon"] = "Filled polygon layer on Map defined by an ordered list of coordinate vertices.",
    ["MapPolyline"] = "Polyline layer on Map connecting an ordered list of coordinate points.",
    ["Confetti"] = "Burst of colored particles on demand via imperative Fire() method.",
    ["Dock"] = "macOS-style icon dock with cursor-proximity magnification.",
    ["Globe"] = "Stylized rotating 3D globe rendered on canvas with dotted lat/long lines.",
    ["MagneticButton"] = "Container that translates toward the cursor within a configurable radius.",
    ["Marquee"] = "Infinitely scrolling horizontal band of children.",
    ["MegaMenu"] = "Full-width dropdown for site-wide navigation with columns.",
    ["Meteors"] = "Falling angled meteor streaks for dramatic decorative backgrounds.",
    ["Mention"] = "Textarea with @-trigger dropdown for mentioning users.",
    ["Menubar"] = "Horizontal menubar with File/Edit-style dropdowns.",
    ["NavigationMenu"] = "Top-level site nav with animated dropdown panels.",
    ["NumberInput"] = "Numeric input with stepper buttons and locale formatting.",
    ["NumberTicker"] = "Animated count-up from zero to target number.",
    ["OtpInput"] = "One-time password input, auto-advances between boxes.",
    ["Overlay"] = "Low-level backdrop primitive for custom popovers and modals.",
    ["Pagination"] = "Page number bar with prev/next and configurable ranges.",
    ["PasswordInput"] = "Password field with show/hide toggle and strength meter.",
    ["PickList"] = "Two-column shuttle picker — move items between lists.",
    ["PopConfirm"] = "Inline 'are you sure?' popover attached to a trigger.",
    ["Popover"] = "Positionable floating panel with anchor and arrow.",
    ["Progress"] = "Linear progress bar with determinate + indeterminate modes.",
    ["PromptInput"] = "Multiline AI prompt textarea with submit + keyboard shortcuts.",
    ["QRCode"] = "Renders a QR code SVG for a string payload.",
    ["RadioGroup"] = "Grouped radio buttons with horizontal or vertical layout.",
    ["Rating"] = "Star rating input with half-star support.",
    ["ReasoningDisplay"] = "Collapsible chain-of-thought block for AI reasoning traces.",
    ["Resizable"] = "Draggable splitter for resizable panel layouts.",
    ["Result"] = "Full-page success/error/info status screen with actions.",
    ["RichTextEditor"] = "WYSIWYG editor wrapping TipTap with Lumeo styling.",
    ["CodeEditor"] = "Source-code editor wrapping CodeMirror 6 with on-demand language packs, dark/light/auto theming, and line numbers.",
    ["Scheduler"] = "Calendar/agenda scheduler wrapping FullCalendar.",
    ["ScrollArea"] = "Styled custom scrollbar container.",
    ["Scrollspy"] = "Highlights the nav item matching the current scroll section.",
    ["Segmented"] = "Pill-shaped tab-like single-select control.",
    ["Select"] = "Native-feeling styled dropdown with search and groups.",
    ["Separator"] = "Horizontal or vertical dividing rule.",
    ["Sheet"] = "Slide-in side panel from left/right/top/bottom.",
    ["ShimmerButton"] = "Button with animated shimmer border beam.",
    ["Sidebar"] = "Collapsible app sidebar with groups, menu, and trigger.",
    ["Skeleton"] = "Pulsing placeholder block for loading states.",
    ["Slider"] = "Range slider with single and dual thumb modes.",
    ["Sortable"] = "Drag-and-drop reorderable list.",
    ["Spacer"] = "Flex-grow spacer that pushes siblings apart.",
    ["SparkCard"] = "Small dashboard card with an inline sparkline chart.",
    ["Sparkline"] = "Inline SVG trend chart primitive — line, area, or bars for tables and KPI strips.",
    ["Ripple"] = "Concentric expanding circles emanating from a container center.",
    ["Sparkles"] = "Decorative sparkle particle animation.",
    ["Spotlight"] = "Container with a radial gradient spotlight that follows the cursor.",
    ["SpeedDial"] = "Floating action button that fans out sub-actions.",
    ["Spinner"] = "Indeterminate loading spinner with size variants.",
    ["Splitter"] = "Resizable split pane for horizontal/vertical layouts.",
    ["Stack"] = "Vertical flex wrapper with gap prop.",
    ["Statistic"] = "Big-number statistic display with label and unit.",
    ["Steps"] = "Numbered step indicator for wizards and progress flows.",
    ["StreamingText"] = "Token-by-token streaming text renderer for AI responses.",
    ["Switch"] = "Toggle switch for boolean settings.",
    ["Table"] = "Minimal styled HTML table with header, row, cell components.",
    ["PivotGrid"] = "Cross-tab / pivot table that summarizes flat data into rows x columns x aggregated measures.",
    ["Tabs"] = "Tabbed content with keyboard nav and animated active indicator.",
    ["TagInput"] = "Input that turns entries into removable tag chips.",
    ["Text"] = "Paragraph text with size, color, weight props.",
    ["TextReveal"] = "Word-by-word reveal animation on scroll.",
    ["TypingAnimation"] = "Renders text one character at a time with optional blinking cursor.",
    ["Textarea"] = "Multiline text input with auto-resize option.",
    ["ThemeSwitcher"] = "Color-scheme picker that writes to ThemeService.",
    ["ThemeToggle"] = "Dark/light mode toggle button.",
    ["TimePicker"] = "Time-of-day picker with 12h/24h formats.",
    ["Timeline"] = "Vertical event timeline with icons and connectors.",
    ["Toast"] = "Notification toast — renders from ToastService queue.",
    ["Toggle"] = "Two-state button with pressed/unpressed styling.",
    ["ToggleGroup"] = "Group of toggles with single or multiple selection.",
    ["ToolCallCard"] = "AI tool-invocation card showing call + result.",
    ["Tooltip"] = "Hover/focus tooltip with arrow and configurable placement.",
    ["Tour"] = "Multi-step spotlight onboarding tour.",
    ["Transfer"] = "Dual-list transfer picker — left/right with arrows.",
    ["TreeSelect"] = "Select input with a hierarchical tree dropdown.",
    ["TreeView"] = "Hierarchical tree with expand/collapse and selection.",
    ["Watermark"] = "Repeating diagonal watermark overlay.",
    // Phase 3 Tier-2 Motion
    ["OrbitingCircles"] = "Children orbit a center point on circular paths with configurable radius and duration.",
    ["AnimatedCircularProgressBar"] = "SVG ring that animates stroke-dashoffset to a target percentage with a centered label.",
    ["WordRotate"] = "Cycles through a list of words with a slide-fade transition on a configurable interval.",
    ["RetroGrid"] = "Perspective-projected grid floor with a subtle scroll animation — the synthwave staple.",
    ["AuroraBackground"] = "Northern-lights multi-layered radial gradient that slowly shifts hue and position via CSS keyframes.",
    ["BackgroundBeams"] = "Network of animated SVG line segments that fade in and out with staggered delays.",
    ["MorphingText"] = "Text morphs between two strings via an SVG blur+contrast filter trick.",
    ["AnimatedGridPattern"] = "SVG grid where individual cells fade in and out at random with stagger.",
    ["ShineBorder"] = "Border with a subtle linear shine that travels around the perimeter.",
    ["MagicCard"] = "Card with a cursor-following radial gradient spotlight and subtle 3D tilt.",
    ["AnimatedSubscribeButton"] = "Multi-state button: idle → loading → success with slide-fade transitions.",
    ["NumberCountUp"] = "Animated count-up to a target number with thousands separators, prefix, and suffix.",
    ["HoverBorderGradient"] = "Border whose conic gradient rotates following cursor position around the element perimeter.",
};

// Heuristic for additional CSS classes we care about in components.
var themedClassPatterns = new[]
{
    "bg-primary", "bg-primary-foreground", "bg-secondary", "bg-secondary-foreground",
    "bg-background", "bg-foreground", "bg-muted", "bg-muted-foreground",
    "bg-accent", "bg-accent-foreground", "bg-destructive", "bg-destructive-foreground",
    "bg-card", "bg-popover", "bg-border", "bg-input", "bg-ring",
    "text-primary", "text-primary-foreground", "text-secondary", "text-secondary-foreground",
    "text-foreground", "text-background", "text-muted", "text-muted-foreground",
    "text-accent", "text-accent-foreground", "text-destructive", "text-destructive-foreground",
    "text-card-foreground", "text-popover-foreground",
    "border-border", "border-input", "border-primary", "border-destructive",
    "ring-ring", "ring-primary", "ring-destructive",
    "fill-primary", "fill-destructive",
};

var cssVarMap = new Dictionary<string, string>
{
    ["bg-primary"] = "--color-primary",
    ["bg-primary-foreground"] = "--color-primary-foreground",
    ["bg-secondary"] = "--color-secondary",
    ["bg-secondary-foreground"] = "--color-secondary-foreground",
    ["bg-background"] = "--color-background",
    ["bg-foreground"] = "--color-foreground",
    ["bg-muted"] = "--color-muted",
    ["bg-muted-foreground"] = "--color-muted-foreground",
    ["bg-accent"] = "--color-accent",
    ["bg-accent-foreground"] = "--color-accent-foreground",
    ["bg-destructive"] = "--color-destructive",
    ["bg-destructive-foreground"] = "--color-destructive-foreground",
    ["bg-card"] = "--color-card",
    ["bg-popover"] = "--color-popover",
    ["bg-border"] = "--color-border",
    ["bg-input"] = "--color-input",
    ["bg-ring"] = "--color-ring",
    ["text-primary"] = "--color-primary",
    ["text-primary-foreground"] = "--color-primary-foreground",
    ["text-secondary"] = "--color-secondary",
    ["text-secondary-foreground"] = "--color-secondary-foreground",
    ["text-foreground"] = "--color-foreground",
    ["text-background"] = "--color-background",
    ["text-muted"] = "--color-muted",
    ["text-muted-foreground"] = "--color-muted-foreground",
    ["text-accent"] = "--color-accent",
    ["text-accent-foreground"] = "--color-accent-foreground",
    ["text-destructive"] = "--color-destructive",
    ["text-destructive-foreground"] = "--color-destructive-foreground",
    ["text-card-foreground"] = "--color-card-foreground",
    ["text-popover-foreground"] = "--color-popover-foreground",
    ["border-border"] = "--color-border",
    ["border-input"] = "--color-input",
    ["border-primary"] = "--color-primary",
    ["border-destructive"] = "--color-destructive",
    ["ring-ring"] = "--color-ring",
    ["ring-primary"] = "--color-primary",
    ["ring-destructive"] = "--color-destructive",
    ["fill-primary"] = "--color-primary",
    ["fill-destructive"] = "--color-destructive",
};

// Known component names (we infer from directory). Used to detect cross-component deps.
// Walk every satellite root so cross-package deps resolve correctly.
var componentDirs = uiRoots
    .Where(Directory.Exists)
    .SelectMany(root => Directory.GetDirectories(root))
    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
    .ToArray();
var knownComponentNames = componentDirs.Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToHashSet(StringComparer.OrdinalIgnoreCase);

// Validation: surface orphan entries in the hand-maintained maps. The Motion
// satellite once had 30+ entries in componentToPackage for components whose
// UI dirs hadn't been added to src/Lumeo.Motion/UI/ yet — those entries
// looked like real components in code review but produced no registry rows.
// Mapping a non-existent name is usually a typo or a stale aspirational
// entry; warn (don't fail) so the developer can fix or remove it.
var orphans = componentToPackage.Keys
    .Concat(categoryMap.Keys)
    .Concat(descriptions.Keys)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(name => !knownComponentNames.Contains(name))
    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (orphans.Length > 0)
{
    Console.Error.WriteLine($"[registry-gen] WARNING: {orphans.Length} hand-maintained map entries point at names with no UI directory:");
    foreach (var o in orphans) Console.Error.WriteLine($"  - {o}");
    Console.Error.WriteLine("[registry-gen] Either add the component dir under the satellite's UI/ folder, or remove the stale entry from Program.cs.");
}

var components = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

foreach (var dir in componentDirs)
{
    var name = Path.GetFileName(dir);
    if (string.IsNullOrEmpty(name)) continue;

    // Resolve which package this component lives in, based on which uiRoot the
    // dir came from — used to compute the relative file paths (registry stores
    // paths relative to the package's src dir so `lumeo add` knows where to fetch).
    var packageRoot = uiRoots.First(r => dir.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    var packageSrcRoot = Path.GetDirectoryName(packageRoot)!; // e.g. src/Lumeo.Charts

    var componentKey = ToKebabCase(name);
    var files = Directory
        .EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
        .Where(p => p.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .Select(p => NormalizePath(Path.GetRelativePath(packageSrcRoot, p)))
        .ToList();

    // Scan for cssVars, dependencies, and package dependencies
    var cssVars = new HashSet<string>(StringComparer.Ordinal);
    var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var packageDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    // Per-component "gotcha" callouts (#87.5): one-line non-obvious-behaviour
    // notes authored as <gotcha>...</gotcha> anywhere in a .razor file. Order
    // is preserved (file order, then source order) and duplicates dropped.
    var gotchas = new List<string>();
    var seenGotchas = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in files)
    {
        // file path is relative to packageSrcRoot (e.g. src/Lumeo.Charts).
        var abs = Path.Combine(packageSrcRoot, file.Replace('/', Path.DirectorySeparatorChar));
        string content;
        // Normalize CRLF -> LF so extracted text (gotchas, etc.) is identical
        // whether the repo is checked out with LF (Linux/CI) or CRLF (Windows
        // autocrlf) — otherwise the registry's string contents drift per platform.
        try { content = File.ReadAllText(abs).Replace("\r\n", "\n").Replace("\r", "\n"); }
        catch { continue; }

        foreach (var cls in themedClassPatterns)
        {
            if (content.Contains(cls, StringComparison.Ordinal) && cssVarMap.TryGetValue(cls, out var v))
                cssVars.Add(v);
        }
        // radius
        if (Regex.IsMatch(content, @"\brounded(-(none|sm|md|lg|xl|2xl|3xl|full))?\b"))
            cssVars.Add("--radius");
        // Package dependencies: detect <Blazicon usage → Blazicons.Lucide
        if (content.Contains("<Blazicon ", StringComparison.Ordinal))
            packageDeps.Add("Blazicons.Lucide");
        // Dependencies: only for .razor, match `<OtherComponent` where OtherComponent is a known sibling.
        if (file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            // Gotcha callouts (#87.5): <gotcha>...</gotcha>, inner text trimmed.
            // Singleline so multi-word / wrapped content matches across newlines.
            foreach (Match gm in Regex.Matches(content, @"<gotcha>(.*?)</gotcha>", RegexOptions.Singleline))
            {
                var note = gm.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(note) && seenGotchas.Add(note))
                    gotchas.Add(note);
            }

            var matches = Regex.Matches(content, @"<([A-Z][A-Za-z0-9]*)\b");
            foreach (Match m in matches)
            {
                var tag = m.Groups[1].Value;
                if (string.Equals(tag, name, StringComparison.OrdinalIgnoreCase)) continue;
                // direct component folder match
                if (knownComponentNames.Contains(tag))
                {
                    deps.Add(ToKebabCase(tag));
                    continue;
                }
                // component whose parent folder starts with tag prefix (e.g. DialogTrigger -> Dialog)
                var parent = knownComponentNames
                    .Where(n => tag.StartsWith(n, StringComparison.OrdinalIgnoreCase) && tag.Length > n.Length)
                    .OrderByDescending(n => n.Length)
                    .FirstOrDefault();
                if (parent != null && !string.Equals(parent, name, StringComparison.OrdinalIgnoreCase))
                    deps.Add(ToKebabCase(parent));
            }
        }
    }

    // Sensible defaults when a component has no themed class usage.
    if (cssVars.Count == 0) cssVars.Add("--color-foreground");

    var category = categoryMap.TryGetValue(name, out var cat) ? cat : "Utility";
    var description = descriptions.TryGetValue(name, out var d) ? d : $"{InsertSpaces(name)} component.";

    var subcategory = SubcategoryInferrer.Infer(name, category);
    // Whether this component has a real docs page in docs/Lumeo.Docs/Pages/Components/.
    // Catalog uses this to skip rendering cards that would 404 on click. Match the
    // page filename CASE-INSENSITIVELY: the component name's casing ("QRCode") can
    // differ from the page file ("QrCodePage.razor"), which under a case-sensitive
    // filesystem (Linux CI) made File.Exists return false and wrongly hid the card.
    var componentsPagesDir = Path.Combine(repoRoot, "docs", "Lumeo.Docs", "Pages", "Components");
    bool PageFileExists(string dir, string fileName) =>
        Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.razor")
            .Any(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    var hasDocsPage = PageFileExists(componentsPagesDir, $"{name}Page.razor")
                      || PageFileExists(Path.Combine(componentsPagesDir, "Charts"), $"{name}ChartPage.razor");
    var entry = new Dictionary<string, object?>
    {
        ["name"] = name,
        ["category"] = category,
        ["subcategory"] = subcategory,
        ["description"] = description,
        ["thumbnail"] = $"/preview-cards/{ToKebabCase(name)}.webp",
        ["hasDocsPage"] = hasDocsPage,
        // Resolution order:
        //   1. Explicit componentToPackage override (legacy / cross-cutting cases).
        //   2. Derived from the satellite folder name — anything under
        //      src/Lumeo.Foo/UI/ maps to "Lumeo.Foo" automatically, so adding a
        //      new satellite needs no edit here.
        //   3. Fallback "Lumeo" (core).
        ["nugetPackage"] = componentToPackage.TryGetValue(name, out var pkg)
            ? pkg
            : (Path.GetFileName(packageSrcRoot) is { Length: > 0 } folder && folder.StartsWith("Lumeo.", StringComparison.Ordinal)
                ? folder
                : "Lumeo"),
        ["files"] = files,
        ["dependencies"] = deps.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        ["packageDependencies"] = packageDeps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
        ["cssVars"] = cssVars.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        ["gotchas"] = gotchas.ToArray(),
        ["registryUrl"] = $"https://lumeo.nativ.sh/registry/{componentKey}.json",
    };
    components[componentKey] = entry;
}

// Satellite wwwroot assets (echarts-interop.js, map.js, …) per package. These are
// NOT in any component's `files` list (they ship as static web assets via the
// satellite NuGet package), so `lumeo add --vendor` needs them enumerated here to
// copy them into the consumer's wwwroot/_content/<package>/. Keyed by package name
// (the satellite src folder, e.g. "Lumeo.Charts"); paths are relative to that root.
var satelliteAssets = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
foreach (var pkgDir in Directory.EnumerateDirectories(Path.Combine(repoRoot, "src")))
{
    // Satellites only — NOT core "Lumeo" (its assets ship via the prebuilt-asset
    // flow, not --vendor). Explicit prefix check, because the Windows "Lumeo.*"
    // glob also matches the extensionless "Lumeo" folder (DOS .* quirk) while Linux
    // does not — that divergence would break the CI registry-freshness gate.
    if (!Path.GetFileName(pkgDir).StartsWith("Lumeo.", StringComparison.Ordinal)) continue;
    var wwwroot = Path.Combine(pkgDir, "wwwroot");
    if (!Directory.Exists(wwwroot)) continue;
    var assets = Directory.EnumerateFiles(wwwroot, "*", SearchOption.AllDirectories)
        .Where(f => !f.EndsWith(".LEGAL.txt", StringComparison.OrdinalIgnoreCase))
        .Select(f => "wwwroot/" + Path.GetRelativePath(wwwroot, f).Replace('\\', '/'))
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToArray();
    if (assets.Length > 0) satelliteAssets[Path.GetFileName(pkgDir)] = assets;
}

var root = new Dictionary<string, object>
{
    ["$schema"] = "https://lumeo.nativ.sh/registry-schema.json",
    ["version"] = lumeoVersion,
    ["generated"] = DateTime.UtcNow.ToString("O"),
    ["components"] = components,
    ["satelliteAssets"] = satelliteAssets,
};

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var json = JsonSerializer.Serialize(root, jsonOpts);
File.WriteAllText(outputPath, json, new UTF8Encoding(false));

// Also write the copy the MCP server bundles + ships. Previously this was only
// synced by the MCP's npm `prebuild` (sync-registry.mjs), so whenever nobody ran
// `npm run build` the committed tools/lumeo-mcp/src/registry.json drifted (it sat a
// month / 149-vs-163 components behind) — a real "MCP is not up to date" bug.
// Writing it here keeps it in lockstep with every regen, and the CI freshness gate
// covers this path too.
var mcpRegistryPath = Path.Combine(repoRoot, "tools", "lumeo-mcp", "src", "registry.json");
if (Directory.Exists(Path.GetDirectoryName(mcpRegistryPath)!))
{
    File.WriteAllText(mcpRegistryPath, json, new UTF8Encoding(false));
    Console.WriteLine($"Wrote registry copy to {mcpRegistryPath}");
}

Console.WriteLine($"Wrote {components.Count} components to {outputPath}");

// ─────── Second pass: full Razor parameter schema → components-api.json ───────
// Consumed by tools/lumeo-mcp so Claude Code gets full schema for ALL components,
// not just the ~30 hand-curated ones.
var componentsApiPath = Path.Combine(repoRoot, "tools", "lumeo-mcp", "src", "components-api.json");

ComponentsApiEmitter.ComponentMeta MetaFor(string name)
{
    var category = categoryMap.TryGetValue(name, out var cat) ? cat : "Utility";
    var description = descriptions.TryGetValue(name, out var desc) ? desc : $"{InsertSpaces(name)} component.";
    var subcategory = SubcategoryInferrer.Infer(name, category);
    var pkg = componentToPackage.TryGetValue(name, out var p) ? p : "Lumeo";

    // Pull files + cssVars from what we already computed in the registry pass.
    var key = ToKebabCase(name);
    if (components.TryGetValue(key, out var entryObj) && entryObj is Dictionary<string, object?> entry)
    {
        var files = entry.TryGetValue("files", out var f) && f is List<string> fl ? fl.ToArray() : Array.Empty<string>();
        var cssVars = entry.TryGetValue("cssVars", out var cv) && cv is string[] cva ? cva : Array.Empty<string>();
        return new ComponentsApiEmitter.ComponentMeta(name, category, subcategory, description, pkg, files, cssVars);
    }
    return new ComponentsApiEmitter.ComponentMeta(name, category, subcategory, description, pkg, Array.Empty<string>(), Array.Empty<string>());
}

try
{
    ComponentsApiEmitter.Emit(
        outputPath: componentsApiPath,
        componentDirs: componentDirs,
        uiRoots: uiRoots,
        metaResolver: MetaFor,
        logger: Console.Error,
        version: lumeoVersion,
        repoRoot: repoRoot);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[components-api] FAILED to emit: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 2;
}

// ─────── Third pass: per-component JSON files for shadcn-style /registry/<key>.json ───────
// The registryUrl field on every component in registry.json points at
//   https://lumeo.nativ.sh/registry/<key>.json
// We now actually emit those files. Each one is self-contained: catalog
// metadata + full Razor parameter schema (params, enums, records, events,
// sub-components). Consumers can fetch a single component without paging
// through the monolithic registry.
//
// Output goes to docs/Lumeo.Docs/wwwroot/registry/ only — Cloudflare Pages
// serves it as https://lumeo.nativ.sh/registry/<key>.json.
// Before rc.40 we ALSO wrote to src/Lumeo/wwwroot/registry/ so the Razor
// SDK would auto-pack the snippets into Lumeo.nupkg as static web assets.
// That added ~3.9 MB to every consumer's package download for a payload
// that NEVER reached their browser — the CLI fetches from the CDN, not
// from _content/Lumeo/registry/. rc.40 drops the duplicate.
try
{
    var apiJson = File.ReadAllText(componentsApiPath, Encoding.UTF8);
    var apiDoc = JsonSerializer.Deserialize<JsonElement>(apiJson);
    var apiComponents = apiDoc.GetProperty("components");

    var perComponentDirs = new[]
    {
        Path.Combine(repoRoot, "docs", "Lumeo.Docs", "wwwroot", "registry"),
    };
    foreach (var dir in perComponentDirs) Directory.CreateDirectory(dir);

    var perComponentCount = 0;
    foreach (var (key, entryObj) in components)
    {
        if (entryObj is not Dictionary<string, object?> entry) continue;

        // Build per-component payload by merging catalog + api schema.
        var payload = new Dictionary<string, object?>(entry, StringComparer.Ordinal)
        {
            ["$schema"] = "https://lumeo.nativ.sh/registry-component-schema.json",
            ["key"] = key,
        };

        // Drop the self-referential URL inside the per-component file —
        // the consumer already knows the URL they fetched it from.
        payload.Remove("registryUrl");

        // Merge in the API schema if we have one for this component.
        if (apiComponents.ValueKind == JsonValueKind.Object)
        {
            // components-api.json is keyed by component name (PascalCase).
            var name = entry.TryGetValue("name", out var n) ? n?.ToString() : null;
            if (name is not null && apiComponents.TryGetProperty(name, out var apiEntry))
            {
                payload["api"] = JsonSerializer.Deserialize<JsonElement>(apiEntry.GetRawText());
                PerComponentEnricher.Enrich(payload, key, name, repoRoot, apiEntry, knownComponentNames, Console.Error);
            }
        }

        var perJson = JsonSerializer.Serialize(payload, jsonOpts);
        foreach (var dir in perComponentDirs)
        {
            File.WriteAllText(Path.Combine(dir, key + ".json"), perJson, new UTF8Encoding(false));
        }
        perComponentCount++;
    }

    Console.Error.WriteLine($"[per-component] Wrote {perComponentCount} component files to {string.Join(" + ", perComponentDirs.Select(d => Path.GetRelativePath(repoRoot, d)))}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[per-component] FAILED to emit: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 3;
}

// ─────── Fourth pass: cdn-deps.json ──────────────────────────────────────────
// Written to two locations:
//   1) docs/Lumeo.Docs/wwwroot/registry/cdn-deps.json  — served by Cloudflare Pages as
//      https://lumeo.nativ.sh/registry/cdn-deps.json (CLI remote fallback, docs site reads it at runtime)
//   2) src/Lumeo/registry/cdn-deps.json                — bundled into Lumeo.nupkg as a static web asset
//      so the CLI can read it from the assembly's embedded resources when offline.
try
{
    var cdnDepsPayload = new Dictionary<string, object>
    {
        ["version"] = "1.0",
        ["generated"] = DateTime.UtcNow.ToString("O"),
        ["deps"] = CdnDeps.All.Select(d => new Dictionary<string, string>
        {
            ["key"]     = d.Key,
            ["package"] = d.Package,
            ["version"] = d.Version,
            ["url"]     = d.Url,
            ["owner"]   = d.Owner,
        }).ToArray(),
    };

    var cdnDepsJson = JsonSerializer.Serialize(cdnDepsPayload, jsonOpts);

    var cdnDepsOutputDirs = new[]
    {
        Path.Combine(repoRoot, "docs", "Lumeo.Docs", "wwwroot", "registry"),
        Path.Combine(repoRoot, "src", "Lumeo", "registry"),
    };

    foreach (var dir in cdnDepsOutputDirs)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "cdn-deps.json"), cdnDepsJson, new UTF8Encoding(false));
    }

    Console.WriteLine($"Wrote cdn-deps.json ({CdnDeps.All.Length} deps) to {string.Join(" + ", cdnDepsOutputDirs.Select(d => Path.GetRelativePath(repoRoot, d)))}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[cdn-deps] FAILED to emit: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 4;
}

return 0;

// --- Helpers ---

static string? FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

static string ReadLockstepVersion(string repoRoot)
{
    var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
    var text = File.ReadAllText(propsPath);
    var m = Regex.Match(text, @"<Version>\s*([^<\s]+)\s*</Version>");
    if (!m.Success)
        throw new InvalidOperationException($"No <Version> element found in {propsPath}");
    return m.Groups[1].Value;
}

static string ToKebabCase(string s)
{
    if (string.IsNullOrEmpty(s)) return s;
    var sb = new StringBuilder(s.Length + 4);
    for (int i = 0; i < s.Length; i++)
    {
        var c = s[i];
        if (char.IsUpper(c))
        {
            if (i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        else sb.Append(c);
    }
    return sb.ToString();
}

static string InsertSpaces(string s)
{
    var sb = new StringBuilder(s.Length + 4);
    for (int i = 0; i < s.Length; i++)
    {
        var c = s[i];
        if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
        sb.Append(c);
    }
    return sb.ToString();
}

static string NormalizePath(string p) => p.Replace('\\', '/');
