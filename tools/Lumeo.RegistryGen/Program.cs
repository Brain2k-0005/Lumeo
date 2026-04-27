using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lumeo.RegistryGen;

// Lumeo Registry Generator
// Scans src/Lumeo/UI/*/ and produces src/Lumeo/registry/registry.json
// Usage: dotnet run --project tools/Lumeo.RegistryGen [--lumeo-root <path>]

var repoRoot = FindRepoRoot(Environment.CurrentDirectory)
               ?? throw new InvalidOperationException("Could not locate Lumeo repo root (no Lumeo.slnx found).");

// Scan core + every satellite package's UI directory. The 2.0-rc.15 split
// moved Chart/DataGrid/RichTextEditor/Scheduler/Gantt out of src/Lumeo/UI/
// into their own satellite folders — without including those, the
// registry would silently miss 7 components and `lumeo add chart` would
// fail with a 404.
var uiRoots = new[]
{
    Path.Combine(repoRoot, "src", "Lumeo", "UI"),
    Path.Combine(repoRoot, "src", "Lumeo.Charts", "UI"),
    Path.Combine(repoRoot, "src", "Lumeo.DataGrid", "UI"),
    Path.Combine(repoRoot, "src", "Lumeo.Editor", "UI"),
    Path.Combine(repoRoot, "src", "Lumeo.Scheduler", "UI"),
    Path.Combine(repoRoot, "src", "Lumeo.Gantt", "UI"),
};

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
    // Editor satellite
    ["RichTextEditor"] = "Lumeo.Editor",
    // Scheduler satellite
    ["Scheduler"] = "Lumeo.Scheduler",
    // Gantt satellite
    ["Gantt"] = "Lumeo.Gantt",
};

// Category map derived from README.md structure.
// Keep in sync with README.md when adding components.
var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    // Layout
    ["Stack"] = "Layout", ["Flex"] = "Layout", ["Grid"] = "Layout", ["Container"] = "Layout",
    ["Center"] = "Layout", ["Spacer"] = "Layout", ["AspectRatio"] = "Layout",
    ["Resizable"] = "Layout", ["ScrollArea"] = "Layout", ["Separator"] = "Layout",
    // Typography
    ["Text"] = "Typography", ["Heading"] = "Typography", ["Link"] = "Typography", ["Code"] = "Typography",
    // Forms
    ["Input"] = "Forms", ["Select"] = "Forms", ["Combobox"] = "Forms", ["DatePicker"] = "Forms",
    ["DateTimePicker"] = "Forms", ["TimePicker"] = "Forms", ["NumberInput"] = "Forms",
    ["PasswordInput"] = "Forms", ["InputMask"] = "Forms", ["Checkbox"] = "Forms",
    ["Switch"] = "Forms", ["RadioGroup"] = "Forms", ["Slider"] = "Forms", ["Toggle"] = "Forms",
    ["ToggleGroup"] = "Forms", ["FileUpload"] = "Forms", ["OtpInput"] = "Forms",
    ["TagInput"] = "Forms", ["ColorPicker"] = "Forms", ["Textarea"] = "Forms", ["Form"] = "Forms",
    ["Mention"] = "Forms", ["Cascader"] = "Forms", ["Segmented"] = "Forms", ["Rating"] = "Forms",
    ["InplaceEditor"] = "Forms",
    // Data Display
    ["Table"] = "Data Display", ["DataTable"] = "Data Display", ["DataGrid"] = "Data Display",
    ["Card"] = "Data Display", ["Badge"] = "Data Display", ["Chip"] = "Data Display",
    ["Avatar"] = "Data Display", ["Calendar"] = "Data Display", ["Descriptions"] = "Data Display",
    ["Statistic"] = "Data Display", ["Timeline"] = "Data Display", ["Steps"] = "Data Display",
    ["Image"] = "Data Display", ["ImageCompare"] = "Data Display", ["TreeView"] = "Data Display",
    ["TreeSelect"] = "Data Display", ["QRCode"] = "Data Display", ["Watermark"] = "Data Display",
    ["List"] = "Data Display", ["Scheduler"] = "Data Display", ["RichTextEditor"] = "Data Display",
    ["Sparkline"] = "Data Display",
    // Feedback
    ["Toast"] = "Feedback", ["Alert"] = "Feedback", ["Progress"] = "Feedback",
    ["Spinner"] = "Feedback", ["Skeleton"] = "Feedback", ["EmptyState"] = "Feedback",
    ["Result"] = "Feedback",
    // Overlay
    ["Dialog"] = "Overlay", ["Sheet"] = "Overlay", ["Drawer"] = "Overlay",
    ["AlertDialog"] = "Overlay", ["Popover"] = "Overlay", ["Tooltip"] = "Overlay",
    ["HoverCard"] = "Overlay", ["ContextMenu"] = "Overlay", ["DropdownMenu"] = "Overlay",
    ["Command"] = "Overlay", ["PopConfirm"] = "Overlay", ["Tour"] = "Overlay",
    ["Overlay"] = "Overlay",
    // Navigation
    ["Tabs"] = "Navigation", ["Breadcrumb"] = "Navigation", ["Pagination"] = "Navigation",
    ["Sidebar"] = "Navigation", ["BottomNav"] = "Navigation", ["Menubar"] = "Navigation",
    ["NavigationMenu"] = "Navigation", ["MegaMenu"] = "Navigation", ["Accordion"] = "Navigation",
    ["Collapsible"] = "Navigation", ["Scrollspy"] = "Navigation", ["BackToTop"] = "Navigation",
    ["Affix"] = "Navigation", ["SpeedDial"] = "Navigation", ["Splitter"] = "Navigation",
    ["Carousel"] = "Navigation",
    // AI
    ["PromptInput"] = "AI", ["StreamingText"] = "AI", ["AgentMessageList"] = "AI",
    ["ToolCallCard"] = "AI", ["ReasoningDisplay"] = "AI",
    // Motion
    ["Marquee"] = "Motion", ["NumberTicker"] = "Motion", ["TextReveal"] = "Motion",
    ["BlurFade"] = "Motion", ["BorderBeam"] = "Motion", ["ShimmerButton"] = "Motion",
    ["Sparkles"] = "Motion",
    // Dashboard
    ["Bento"] = "Dashboard", ["KpiCard"] = "Dashboard", ["SparkCard"] = "Dashboard",
    ["Delta"] = "Dashboard", ["PickList"] = "Dashboard",
    // Drag & Drop
    ["Kanban"] = "Drag & Drop", ["Sortable"] = "Drag & Drop", ["Transfer"] = "Drag & Drop",
    ["Filter"] = "Data Display",
    // Charts (subgroup of Data Display per docs nav v2)
    ["Chart"] = "Data Display",
    // Utility
    ["Button"] = "Forms",
    ["Icon"] = "Utility", ["Kbd"] = "Utility", ["Label"] = "Utility",
    ["ThemeSwitcher"] = "Utility", ["ThemeToggle"] = "Utility",
};

// One-line descriptions (hand-written-ish, name-based).
var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
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
    ["DataGrid"] = "Enterprise grid: sort, filter, inline edit, group, pin, virtualize, export.",
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
    ["Marquee"] = "Infinitely scrolling horizontal band of children.",
    ["MegaMenu"] = "Full-width dropdown for site-wide navigation with columns.",
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
    ["Sparkles"] = "Decorative sparkle particle animation.",
    ["SpeedDial"] = "Floating action button that fans out sub-actions.",
    ["Spinner"] = "Indeterminate loading spinner with size variants.",
    ["Splitter"] = "Resizable split pane for horizontal/vertical layouts.",
    ["Stack"] = "Vertical flex wrapper with gap prop.",
    ["Statistic"] = "Big-number statistic display with label and unit.",
    ["Steps"] = "Numbered step indicator for wizards and progress flows.",
    ["StreamingText"] = "Token-by-token streaming text renderer for AI responses.",
    ["Switch"] = "Toggle switch for boolean settings.",
    ["Table"] = "Minimal styled HTML table with header, row, cell components.",
    ["Tabs"] = "Tabbed content with keyboard nav and animated active indicator.",
    ["TagInput"] = "Input that turns entries into removable tag chips.",
    ["Text"] = "Paragraph text with size, color, weight props.",
    ["TextReveal"] = "Word-by-word reveal animation on scroll.",
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

    foreach (var file in files)
    {
        // file path is relative to packageSrcRoot (e.g. src/Lumeo.Charts).
        var abs = Path.Combine(packageSrcRoot, file.Replace('/', Path.DirectorySeparatorChar));
        string content;
        try { content = File.ReadAllText(abs); }
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
    var entry = new Dictionary<string, object?>
    {
        ["name"] = name,
        ["category"] = category,
        ["subcategory"] = subcategory,
        ["description"] = description,
        ["thumbnail"] = $"/preview-cards/{ToSlug(name)}.png",
        ["nugetPackage"] = componentToPackage.TryGetValue(name, out var pkg) ? pkg : "Lumeo",
        ["files"] = files,
        ["dependencies"] = deps.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        ["packageDependencies"] = packageDeps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
        ["cssVars"] = cssVars.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        ["registryUrl"] = $"https://lumeo.nativ.sh/registry/{componentKey}.json",
    };
    components[componentKey] = entry;
}

var root = new Dictionary<string, object>
{
    ["$schema"] = "https://lumeo.nativ.sh/registry-schema.json",
    ["version"] = "2.0.0",
    ["generated"] = DateTime.UtcNow.ToString("O"),
    ["components"] = components,
};

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var json = JsonSerializer.Serialize(root, jsonOpts);
File.WriteAllText(outputPath, json, new UTF8Encoding(false));

Console.WriteLine($"Wrote {components.Count} components to {outputPath}");
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

static string ToSlug(string name) =>
    System.Text.RegularExpressions.Regex.Replace(name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
